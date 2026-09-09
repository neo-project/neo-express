import assert from "node:assert/strict";
import test from "node:test";
import stripAnsi from "./stripAnsi";

test("stripAnsi removes neoxp error coloring", () => {
  const raw =
    "\u001b[1m\u001b[31m\u001b[40mSystem.InvalidOperationException: contract \"TestContract\" not found\u001b[0m";
  assert.equal(
    stripAnsi(raw),
    'System.InvalidOperationException: contract "TestContract" not found'
  );
});

test("stripAnsi leaves plain text unchanged", () => {
  assert.equal(stripAnsi("ok"), "ok");
});
