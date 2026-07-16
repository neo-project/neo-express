import assert from "node:assert/strict";
import test from "node:test";

import getContractTreeCommand from "./contractTreeCommand";

test("getContractTreeCommand opens deployed contracts", () => {
  assert.deepEqual(getContractTreeCommand({ hash: "0x1234" }), {
    command: "neo3-visual-devtracker.tracker.openContract",
    arguments: [{ hash: "0x1234" }],
    title: "0x1234",
  });
});

test("getContractTreeCommand does not open workspace contracts on selection", () => {
  assert.equal(
    getContractTreeCommand({ path: "/workspace/Sample.nef" }),
    undefined
  );
});
