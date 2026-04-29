import assert from "node:assert/strict";
import test from "node:test";

import getTrustedWorkspaceServerListFiles from "./serverListWorkspaceTrust";

test("getTrustedWorkspaceServerListFiles keeps workspace server lists in trusted workspaces", () => {
  assert.deepEqual(
    getTrustedWorkspaceServerListFiles(["/workspace/neo-servers.json"], true),
    ["/workspace/neo-servers.json"]
  );
});

test("getTrustedWorkspaceServerListFiles ignores workspace server lists in untrusted workspaces", () => {
  assert.deepEqual(
    getTrustedWorkspaceServerListFiles(["/workspace/neo-servers.json"], false),
    []
  );
});
