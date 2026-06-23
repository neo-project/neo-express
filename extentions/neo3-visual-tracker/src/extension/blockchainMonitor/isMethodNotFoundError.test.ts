import assert from "node:assert/strict";
import test from "node:test";

import isMethodNotFoundError from "./isMethodNotFoundError";

test("detects a method-not-found error message", () => {
  assert.equal(isMethodNotFoundError(new Error("Method not found")), true);
  assert.equal(
    isMethodNotFoundError({ message: "RPC error: Method not found (-32601)" }),
    true
  );
});

test("treats an error with an unrelated message as transient", () => {
  assert.equal(isMethodNotFoundError(new Error("socket hang up")), false);
});

test("treats an error with no usable message as transient", () => {
  // Regression: the previous `e.message?.indexOf(...) !== -1` evaluated to
  // `undefined !== -1` (true) when message was absent, wrongly classifying a
  // transient/messageless error as method-not-found.
  assert.equal(isMethodNotFoundError({}), false);
  assert.equal(isMethodNotFoundError(undefined), false);
  assert.equal(isMethodNotFoundError(null), false);
  assert.equal(isMethodNotFoundError("boom"), false);
});
