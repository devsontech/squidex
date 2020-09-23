﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Squidex.Infrastructure.Reflection;
using Squidex.Infrastructure.Tasks;

namespace Squidex.Infrastructure.EventSourcing.Grains
{
    internal sealed class BatchSubscriber : IEventSubscriber
    {
        private readonly ITargetBlock<Job> pipelineStart;
        private readonly IEventDataFormatter eventDataFormatter;
        private readonly IEventSubscription eventSubscription;
        private readonly IDataflowBlock pipelineEnd;

        public object Sender
        {
            get { return eventSubscription.Sender; }
        }

        private sealed class Job
        {
            public StoredEvent? StoredEvent { get; set; }

            public Exception? Exception { get; set; }

            public Envelope<IEvent>? Event { get; set; }

            public bool ShouldHandle { get; set; }

            public object Sender { get; set; }
        }

        public BatchSubscriber(
            EventConsumerGrain grain,
            IEventDataFormatter eventDataFormatter,
            IEventConsumer eventConsumer,
            Func<IEventSubscriber, IEventSubscription> factory,
            TaskScheduler scheduler)
        {
            this.eventDataFormatter = eventDataFormatter;

            var batchSize = Math.Max(1, eventConsumer!.BatchSize);
            var batchDelay = Math.Max(100, eventConsumer.BatchDelay);

            var parse = new TransformBlock<Job, Job>(job =>
            {
                if (job.StoredEvent != null)
                {
                    job.ShouldHandle = eventConsumer.Handles(job.StoredEvent);
                }

                if (job.ShouldHandle)
                {
                    try
                    {
                        job.Event = ParseKnownEvent(job.StoredEvent!);
                    }
                    catch (Exception ex)
                    {
                        job.Exception = ex;
                    }
                }

                return job;
            }, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = batchSize,
                MaxDegreeOfParallelism = 1,
                MaxMessagesPerTask = 1
            });

            var buffer = AsyncHelper.CreateBatchBlock<Job>(batchSize, batchDelay, new GroupingDataflowBlockOptions
            {
                BoundedCapacity = batchSize * 2
            });

            var handle = new ActionBlock<IList<Job>>(async jobs =>
            {
                foreach (var jobsBySender in jobs.GroupBy<Job, object>(x => x.Sender))
                {
                    var sender = jobsBySender.Key;

                    if (ReferenceEquals(sender, eventSubscription.Sender))
                    {
                        var exception = jobs.FirstOrDefault(x => x.Exception != null)?.Exception;

                        if (exception != null)
                        {
                            await grain.OnErrorAsync(Sender, exception);
                        }
                        else
                        {
                            await grain.OnEventsAsync(Sender, GetEvents(jobsBySender), GetPosition(jobsBySender));
                        }
                    }
                }
            },
            new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 2,
                MaxDegreeOfParallelism = 1,
                MaxMessagesPerTask = 1,
                TaskScheduler = scheduler
            });

            parse.LinkTo(buffer, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });

            buffer.LinkTo(handle, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });

            pipelineStart = parse;
            pipelineEnd = handle;

            eventSubscription = factory(this);
        }

        private static List<Envelope<IEvent>> GetEvents(IEnumerable<Job> jobsBySender)
        {
            return jobsBySender.NotNull(x => x.Event).ToList();
        }

        private static string GetPosition(IEnumerable<Job> jobsBySender)
        {
            return jobsBySender.Last().StoredEvent!.EventPosition;
        }

        public Task CompleteAsync()
        {
            pipelineStart.Complete();

            return pipelineEnd.Completion;
        }

        public void WakeUp()
        {
            eventSubscription.WakeUp();
        }

        public void Unsubscribe()
        {
            eventSubscription.Unsubscribe();
        }

        private Envelope<IEvent>? ParseKnownEvent(StoredEvent storedEvent)
        {
            try
            {
                var @event = eventDataFormatter.Parse(storedEvent.Data);

                @event.SetEventPosition(storedEvent.EventPosition);
                @event.SetEventStreamNumber(storedEvent.EventStreamNumber);

                return @event;
            }
            catch (TypeNameNotFoundException)
            {
                return null;
            }
        }

        public Task OnEventAsync(IEventSubscription subscription, StoredEvent storedEvent)
        {
            var job = new Job
            {
                Sender = subscription,
                StoredEvent = storedEvent
            };

            return pipelineStart.SendAsync(job);
        }

        public Task OnErrorAsync(IEventSubscription subscription, Exception exception)
        {
            var job = new Job
            {
                Sender = subscription,
                Exception = exception
            };

            return pipelineStart.SendAsync(job);
        }
    }
}
