// Copyright (C) 2015-2026 The Neo Project.
//
// This file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.

"use strict";

const path = require("path");
const unzipper = require("unzipper");

const packageDir = path.resolve(__dirname, "..", "deps", "nxp");
const packagePath = path.join(packageDir, "nxp.nupkg");

unzipper.Open.file(packagePath)
  .then((directory) => directory.extract({ path: packageDir }))
  .catch((error) => {
    console.error(`Failed to extract ${packagePath}: ${error.message}`);
    process.exitCode = 1;
  });
