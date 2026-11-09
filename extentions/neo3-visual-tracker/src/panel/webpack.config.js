//@ts-check

"use strict";

const path = require("path");
const webpack = require("webpack");

/**@type {import('webpack').Configuration}*/
const config = {
  context: path.join(__dirname, "..", ".."),
  entry: "./src/panel/index.tsx",
  output: {
    path: path.join(__dirname, "..", "..", "dist", "panel"),
    filename: "index.js",
    devtoolModuleFilenameTemplate: "file://[absolute-resource-path]",
  },
  devtool: false,
  plugins: [
    new webpack.SourceMapDevToolPlugin({
      filename: "[file].map",
      append:
        "\n//# sourceMappingURL=file://" +
        path.resolve(__dirname, "..", "..", "dist", "panel") +
        "/[url]",
    }),
  ],
  resolve: {
    extensions: [".tsx", ".ts", ".js"],
    fallback: {
      crypto: false,
      stream: require.resolve("stream-browserify"),
    },
  },
  module: {
    rules: [
      {
        test: /\.css$/i,
        use: ["style-loader", "css-loader"],
      },
      {
        test: /\.(ttf|woff2?)$/i,
        type: "asset/resource",
      },
      {
        test: /\.(png|jpe?g|svg|html)$/i,
        use: [
          {
            loader: "file-loader",
            options: { name: "[name].[ext]" },
          },
        ],
      },
      {
        test: /\.tsx?$/,
        exclude: /node_modules/,
        use: [
          {
            loader: "ts-loader",
          },
        ],
      },
    ],
  },
};

module.exports = config;
