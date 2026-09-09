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
    /const wasRunning = neoExpressInstanceManager\.isRunning\(blockchainIdentifier\);[\s\S]*await neoExpressInstanceManager\.stopAll\(blockchainIdentifier\);/
  );
  assert.match(
    neoExpressCommands,
    /const wasRunning = neoExpressInstanceManager\.isRunning\(identifier\);[\s\S]*await neoExpressInstanceManager\.stopAll\(identifier\);/
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

test("Reset stops leftover nodes, does not restart after failure, and does not hang the status bar", () => {
  const neoExpressCommands = readFileSync(
    join(__dirname, "neoExpressCommands.ts"),
    "utf8"
  );
  assert.match(neoExpressCommands, /"reset",\s+"-f",\s+"--all"/);
  assert.match(neoExpressCommands, /isNodeStillRunningError/);
  assert.match(
    neoExpressCommands,
    /if \(output\.isError \|\| isFailedTx\(output\.message\) \|\| isNodeStillRunningError\(output\.message\)\) \{\s*return;/
  );
  assert.doesNotMatch(
    neoExpressCommands,
    /finally \{\s*if \(wasRunning\) \{\s*report\("Restarting node\.\.\."\)/
  );

  const txPrep = readFileSync(join(__dirname, "../util/txPrep.ts"), "utf8");
  assert.doesNotMatch(txPrep, /createStatusBarItem/);
  assert.match(txPrep, /ProgressLocation\.Window/);

  const neoExpress = readFileSync(
    join(__dirname, "../neoExpress/neoExpress.ts"),
    "utf8"
  );
  assert.match(neoExpress, /START_TIMEOUT_MS/);
  assert.match(neoExpress, /watchForExpressStart\(pty, START_TIMEOUT_MS\)/);

  const watchStart = readFileSync(
    join(__dirname, "../neoExpress/watchExpressStart.ts"),
    "utf8"
  );
  assert.match(watchStart, /onDidExit/);
});
