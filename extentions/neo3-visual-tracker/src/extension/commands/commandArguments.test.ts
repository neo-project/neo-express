import assert from "node:assert/strict";
import test from "node:test";

import { sanitizeCommandArguments } from "./commandArguments";

test("sanitizeCommandArguments rejects unsupported path arguments", async () => {
  await assert.rejects(
    sanitizeCommandArguments({ path: "/tmp/contract.nef" }),
    /Command URI argument 'path' is not supported/
  );
});

test("sanitizeCommandArguments rejects unsupported blockchain identifiers", async () => {
  await assert.rejects(
    sanitizeCommandArguments({ blockchainIdentifier: "private-net" }),
    /Command URI argument 'blockchainIdentifier' is not supported/
  );
});
