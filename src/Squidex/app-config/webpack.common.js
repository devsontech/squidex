﻿// ReSharper disable InconsistentNaming
// ReSharper disable PossiblyUnassignedProperty

        var webpack = require('webpack'),
               path = require('path'),
  HtmlWebpackPlugin = require('html-webpack-plugin'),
  ExtractTextPlugin = require('extract-text-webpack-plugin'),
            helpers = require('./helpers');

module.exports = {
    /**
     * The entry point for the bundle
     * Our Angular.js app
     *
     * See: http://webpack.github.io/docs/configuration.html#entry
     */
    entry: {
        'polyfills': './app/polyfills.ts',
           'vendor': './app/vendor.ts',
              'app': './app/main.ts'
    },

    /**
     * Options affecting the resolving of modules.
     *
     * See: http://webpack.github.io/docs/configuration.html#resolve
     */
    resolve: {
        /**
         * An array of extensions that should be used to resolve modules.
         *
         * See: http://webpack.github.io/docs/configuration.html#resolve-extensions
         */
        extensions: ['.js', '.ts', '.css', '.scss'],
        modules: [
            helpers.root('app'),
            helpers.root('app', 'theme'),
            helpers.root('app-libs'),
            helpers.root('node_modules')
        ],
    },

    /*
     * Options affecting the normal modules.
     *
     * See: http://webpack.github.io/docs/configuration.html#module
     */
    module: {
        /**
         * An array of automatically applied loaders.
         *
         * IMPORTANT: The loaders here are resolved relative to the resource which they are applied to.
         * This means they are not resolved relative to the configuration file.
         *
         * See: http://webpack.github.io/docs/configuration.html#module-loaders
         */
        loaders: [
            {
                test: /\.ts$/,
                loaders: ['awesome-typescript', helpers.root('app-config', 'auto-loader') + '?[file].html=template&[file].scss=styles', 'tslint']
            }, {
                test: /\.html$/,
                loader: 'html'
            }, {
                test: /\.(png|jpe?g|gif|svg|woff|woff2|ttf|eot|ico)(\?.*$|$)/,
                loader: 'file?name=assets/[name].[hash].[ext]'
            }, {
                test: /\.css$/,
                loader: ExtractTextPlugin.extract({ fallbackLoader: 'style', loader: 'css?sourceMap' })
            }, {
                test: /\.scss$/,
                exclude: helpers.root('app', 'theme'),
                loaders: ['raw', 'sass']
            }
        ]
    },

    plugins: [
        new webpack.LoaderOptionsPlugin({
            options: {
                tslint: {
                    /**
                    * Run tslint in production build and fail if there is one warning.
                    * 
                    * See: https://github.com/wbuchwalter/tslint-loader
                    */
                    emitErrors: false,
                    /**
                    * Share the configuration file with the IDE
                    */
                    configuration: require('./../tslint.json')
                },
                sassLoader: {
                    includePaths: [helpers.root('app', 'theme')]
                },
                context: '/'
            }
        }),

        /**
         * Shares common code between the pages.
         * It identifies common modules and put them into a commons chunk.
         *
         * See: https://webpack.github.io/docs/list-of-plugins.html#commonschunkplugin
         */
        new webpack.optimize.CommonsChunkPlugin({
            name: ['app', 'vendor', 'polyfills']
        }),

        /**
         * Simplifies creation of HTML files to serve your webpack bundles.
         * This is especially useful for webpack bundles that include a hash in the filename
         * which changes every compilation.
         *
         * See: https://github.com/ampedandwired/html-webpack-plugin
         */
        new HtmlWebpackPlugin({
            template: 'wwwroot/index.html'
        }),

        new webpack.ContextReplacementPlugin(/moment[\/\\]locale$/, /en/),

        /**
         * Shim additional libraries
         * 
         * See: https://webpack.github.io/docs/shimming-modules.html
         */
        new webpack.ProvidePlugin({
            // Mouse trap handles shortcut management
            'Mousetrap': 'mousetrap/mousetrap'
        })
    ]
};