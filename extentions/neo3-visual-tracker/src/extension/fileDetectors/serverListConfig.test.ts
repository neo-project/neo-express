import assert from "node:assert/strict";
import test from "node:test";

import parseServerListConfig from "./serverListConfig";

test("parseServerListConfig keeps only string blockchain names and RPC URLs", () => {
  const parsed = parseServerListConfig({
    "neo-blockchain-names": {
      " 0xABC123 ": " MainNet ",
      "0xdef456": { label: "TestNet" },
      "0x789": 42,
    },
    "neo-rpc-uris": [
      " https://rpc.example/ ",
      { network: "testnet", rpcUris: ["https://test.example/"] },
      9007199254740992,
      "",
      null,
    ],
  });

  assert.deepEqual(parsed.blockchainNames, { "0xabc123": "MainNet" });
  assert.deepEqual(parsed.rpcUrls, ["https://rpc.example/"]);
});

test("parseServerListConfig tolerates non-object top-level values", () => {
  assert.deepEqual(parseServerListConfig(null), {
    blockchainNames: {},
    rpcUrls: [],
  });
  assert.deepEqual(parseServerListConfig(9007199254740992), {
    blockchainNames: {},
    rpcUrls: [],
  });
});
