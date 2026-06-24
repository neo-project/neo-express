import assert from "node:assert/strict";
import test from "node:test";

import wellKnownContractsCacheKey from "./wellKnownContractsCacheKey";

test("wellKnownContractsCacheKey builds a version-specific key", () => {
  assert.equal(wellKnownContractsCacheKey("3.7.0"), "wellKnownContracts_3.7.0");
});

test("wellKnownContractsCacheKey trims surrounding whitespace", () => {
  assert.equal(wellKnownContractsCacheKey("  3.7.0\n"), "wellKnownContracts_3.7.0");
});

test("wellKnownContractsCacheKey distinguishes different versions", () => {
  // Under the previous substring(256) bug both collapsed to the constant
  // "wellKnownContracts_", so the cache was never version-specific.
  assert.notEqual(
    wellKnownContractsCacheKey("3.7.0"),
    wellKnownContractsCacheKey("3.8.0")
  );
});

test("wellKnownContractsCacheKey caps the version at 256 characters", () => {
  const key = wellKnownContractsCacheKey("v".repeat(300));
  assert.equal(key, `wellKnownContracts_${"v".repeat(256)}`);
});
