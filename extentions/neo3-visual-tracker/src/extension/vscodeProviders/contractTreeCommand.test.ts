import assert from "node:assert/strict";
import test from "node:test";

import getContractTreeCommand, {
  ContractTreeItemData,
  getWorkspaceContractPath,
} from "./contractTreeCommand";

test("getContractTreeCommand opens deployed contracts", () => {
  assert.deepEqual(getContractTreeCommand({ name: "Sample", hash: "0x1234" }), {
    command: "neo3-visual-devtracker.tracker.openContract",
    arguments: [{ hash: "0x1234" }],
    title: "0x1234",
  });
});

test("getContractTreeCommand does not open workspace contracts on selection", () => {
  assert.equal(
    getContractTreeCommand({ name: "Sample", path: "/workspace/Sample.nef" }),
    undefined
  );
});

test("workspace contract tree items expose their trusted file path", () => {
  const item = new ContractTreeItemData(
    "Sample",
    undefined,
    undefined,
    "/workspace/Sample.nef"
  );

  assert.equal(getWorkspaceContractPath(item), "/workspace/Sample.nef");
});

test("plain command arguments cannot supply a trusted workspace path", () => {
  assert.equal(
    getWorkspaceContractPath({
      name: "Sample",
      path: "/outside-workspace/Sample.nef",
    }),
    undefined
  );
});
