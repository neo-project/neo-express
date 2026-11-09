import assert from "node:assert/strict";
import test from "node:test";

import {
  isEmptyArgument,
  isSaveShortcut,
  normalizeArguments,
  stringToValue,
  valueToString,
} from "./argumentValues";

test("isEmptyArgument preserves valid falsy arguments", () => {
  assert.equal(isEmptyArgument(0), false);
  assert.equal(isEmptyArgument(false), false);
  assert.equal(isEmptyArgument(""), true);
  assert.equal(isEmptyArgument(null), true);
  assert.equal(isEmptyArgument(undefined), true);
});

test("normalizeArguments preserves falsy and positional arguments", () => {
  assert.deepEqual(normalizeArguments([0, false, "", ""], 0), [0, false]);
  assert.deepEqual(normalizeArguments(["", 1, ""], 0), ["", 1]);
  assert.deepEqual(normalizeArguments([0], 3), [0, "", ""]);
});

test("argument values round trip through the input representation", () => {
  assert.equal(valueToString(0), "0");
  assert.equal(valueToString(false), "false");
  assert.equal(stringToValue(valueToString(0)), 0);
  assert.equal(stringToValue(valueToString(false)), false);
});

test("isSaveShortcut recognizes save on macOS, Windows, and Linux", () => {
  assert.equal(
    isSaveShortcut({ ctrlKey: false, key: "s", metaKey: true }),
    true
  );
  assert.equal(
    isSaveShortcut({ ctrlKey: true, key: "S", metaKey: false }),
    true
  );
  assert.equal(
    isSaveShortcut({ ctrlKey: false, key: "k", metaKey: true }),
    false
  );
  assert.equal(
    isSaveShortcut({ ctrlKey: false, key: "s", metaKey: false }),
    false
  );
});
