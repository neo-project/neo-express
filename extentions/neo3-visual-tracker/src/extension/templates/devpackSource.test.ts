import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { join } from "node:path";
import test from "node:test";

const repoRoot = join(__dirname, "../../../../../");

test("C# starter sources match the pinned neo-devpack-dotnet commit", () => {
  const result = spawnSync(
    process.execPath,
    ["scripts/sync-devpack-starters.mjs", "--check"],
    { cwd: repoRoot, encoding: "utf8" }
  );
  assert.equal(
    result.status,
    0,
    `${result.stdout ?? ""}\n${result.stderr ?? ""}`
  );
});
