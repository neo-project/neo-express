import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

test("Visual Tracker stops a running Neo Express instance before offline checkpoint operations", () => {
  const neoExpressCommands = readFileSync(
    join(__dirname, "neoExpressCommands.ts"),
    "utf8"
  );
  assert.match(
    neoExpressCommands,
    /const wasRunning = neoExpressInstanceManager\.isRunning\(blockchainIdentifier\);\s+await neoExpressInstanceManager\.stopAll\(blockchainIdentifier\);/s
  );
  assert.match(
    neoExpressCommands,
    /const wasRunning = neoExpressInstanceManager\.isRunning\(identifier\);\s+await neoExpressInstanceManager\.stopAll\(identifier\);/s
  );

  const neoExpressInstanceManager = readFileSync(
    join(__dirname, "../neoExpress/neoExpressInstanceManager.ts"),
    "utf8"
  );
  assert.match(
    neoExpressInstanceManager,
    /"stop",\s+"--all",\s+"-i",\s+target\.configPath/s
  );
});
