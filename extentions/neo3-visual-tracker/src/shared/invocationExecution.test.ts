import assert from "node:assert/strict";
import test from "node:test";

import {
  areInvocationStepsReady,
  isLiveDebugWitnessScopeSupported,
  isWitnessScope,
  resolveSelectedAccount,
  toInvocationAccounts,
} from "./invocationExecution";

test("toInvocationAccounts returns stable alphabetic account options", () => {
  assert.deepEqual(
    toInvocationAccounts({ genesis: "0x02", alice: "0x01" }),
    [
      { name: "alice", address: "0x01" },
      { name: "genesis", address: "0x02" },
    ]
  );
});

test("resolveSelectedAccount preserves an available selection", () => {
  const accounts = toInvocationAccounts({ alice: "0x01", bob: "0x02" });
  assert.equal(resolveSelectedAccount(accounts, "bob"), "bob");
});

test("resolveSelectedAccount falls back when the selection disappears", () => {
  const accounts = toInvocationAccounts({ alice: "0x01", bob: "0x02" });
  assert.equal(resolveSelectedAccount(accounts, "missing"), "alice");
  assert.equal(resolveSelectedAccount([], "missing"), null);
});

test("isWitnessScope accepts only supported Neo witness scopes", () => {
  assert.equal(isWitnessScope("CalledByEntry"), true);
  assert.equal(isWitnessScope("Global"), true);
  assert.equal(isWitnessScope("None"), true);
  assert.equal(isWitnessScope("CustomContracts"), false);
});

test("areInvocationStepsReady requires every contract and operation", () => {
  assert.equal(areInvocationStepsReady([]), false);
  assert.equal(
    areInvocationStepsReady([{ contract: "#Sample", operation: "" }]),
    false
  );
  assert.equal(
    areInvocationStepsReady([
      { contract: "#Sample", operation: "one" },
      { contract: "0x1234", operation: "two" },
    ]),
    true
  );
});

test("live debugging only supports CalledByEntry witness scope", () => {
  assert.equal(isLiveDebugWitnessScopeSupported("CalledByEntry"), true);
  assert.equal(isLiveDebugWitnessScopeSupported("Global"), false);
  assert.equal(isLiveDebugWitnessScopeSupported("None"), false);
});
