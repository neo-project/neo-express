import assert from "node:assert/strict";
import test from "node:test";

import {
  MAINNET_GENESIS,
  TESTNET_GENESIS,
  knownGenesisHashForSeed,
} from "./publicChainSeeds";

test("known seed URLs map to MainNet/TestNet without RPC", () => {
  assert.equal(
    knownGenesisHashForSeed("http://seed1.neo.org:10332"),
    MAINNET_GENESIS
  );
  assert.equal(
    knownGenesisHashForSeed("http://seed2t4.neo.org:20332"),
    TESTNET_GENESIS
  );
  assert.equal(knownGenesisHashForSeed("http://127.0.0.1:50012"), undefined);
});
