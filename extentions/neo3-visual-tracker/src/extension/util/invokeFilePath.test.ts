import assert from "node:assert/strict";
import test from "node:test";

import { resolveInvokeFilePath } from "./invokeFilePath";

test("resolveInvokeFilePath reuses an existing invocation file for a workspace contract", () => {
  const existing = new Set(["/ws/invoke-files/Sample.neo-invoke.json"]);
  const result = resolveInvokeFilePath(
    "/ws/invoke-files",
    "Sample",
    (path) => existing.has(path)
  );
  assert.equal(result.path, "/ws/invoke-files/Sample.neo-invoke.json");
  assert.equal(result.reuse, true);
});

test("resolveInvokeFilePath creates the canonical name when none exists", () => {
  const result = resolveInvokeFilePath(
    "/ws/invoke-files",
    "Sample",
    () => false
  );
  assert.equal(result.path, "/ws/invoke-files/Sample.neo-invoke.json");
  assert.equal(result.reuse, false);
});
