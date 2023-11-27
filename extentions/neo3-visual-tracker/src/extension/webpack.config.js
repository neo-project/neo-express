//@ts-check

"use strict";

const path = require("path");

/**@type {import('webpack').Configuration}*/
const config = {
  context: path.join(__dirname, "..", ".."),
  target: "node",
  entry: "./src/extension/index.ts",
  output: {
    path: path.join(__dirname, "..", "..", "dist", "extension"),
    filename: "index.js",
    libraryTarget: "commonjs2",
    devtoolModuleFilenameTemplate: "[absolute-resource-path]",
  },
  devtool: "source-map",
  externals: {
    vscode: "commonjs vscode",
  },
  resolve: {
    extensions: [".ts", ".js"],
    fallback: {
      "bignumber.js": require.resolve("bignumber.js"),
      crypto: false,
    },
  },
  module: {
    rules: [
      {
        test: /\.ts$/,
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
