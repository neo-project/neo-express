// Copyright (C) 2015-2026 The Neo Project.
//
// This file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

"use strict";

const fs = require("fs");
const http = require("http");
const https = require("https");
const path = require("path");

const version = process.env.npm_package_version;
if (!version) {
  console.error("npm_package_version is not set.");
  process.exit(1);
}

const outputDir = path.join(__dirname, "..", "deps", "nxp");
const outputPath = path.join(outputDir, "nxp.nupkg");
const packageUrl = `https://www.nuget.org/api/v2/package/Neo.Express/${version}`;
const localPackagePath = process.env.NEO_EXPRESS_NUPKG;

fs.rmSync(outputDir, { recursive: true, force: true });
fs.mkdirSync(outputDir, { recursive: true });

if (localPackagePath) {
  fs.copyFileSync(path.resolve(localPackagePath), outputPath);
  process.exit(0);
}

function download(url, redirectsLeft = 5) {
  return new Promise((resolve, reject) => {
    const client = url.startsWith("https:") ? https : http;
    const request = client.get(
      url,
      { headers: { "User-Agent": "neo3-visual-tracker-package" } },
      (response) => {
        const { statusCode, headers } = response;
        const redirect =
          statusCode &&
          [301, 302, 303, 307, 308].includes(statusCode) &&
          headers.location;

        if (redirect) {
          response.resume();
          if (redirectsLeft === 0) {
            reject(new Error(`Too many redirects while downloading ${packageUrl}`));
            return;
          }
          resolve(download(new URL(redirect, url).toString(), redirectsLeft - 1));
          return;
        }

        if (statusCode !== 200) {
          response.resume();
          reject(new Error(`Download failed with HTTP ${statusCode}: ${url}`));
          return;
        }

        const file = fs.createWriteStream(outputPath);
        response.pipe(file);
        file.on("finish", () => file.close(resolve));
        file.on("error", reject);
      }
    );

    request.on("error", reject);
  });
}

download(packageUrl).catch((error) => {
  console.error(error.message);
  process.exit(1);
});
