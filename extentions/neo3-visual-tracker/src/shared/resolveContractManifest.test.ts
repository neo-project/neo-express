import assert from "node:assert/strict";
import test from "node:test";

import AutoCompleteData from "./autoCompleteData";
import {
  manifestMethods,
  resolveContractManifest,
  stripContractPrefix,
} from "./resolveContractManifest";

function data(partial: Partial<AutoCompleteData>): AutoCompleteData {
  return {
    addressNames: {},
    contractManifests: {},
    contractNames: {},
    contractPaths: {},
    wellKnownAddresses: {},
    ...partial,
  };
}

test("stripContractPrefix removes hash alias", () => {
  assert.equal(stripContractPrefix("#Foo"), "Foo");
  assert.equal(stripContractPrefix("Foo"), "Foo");
});

test("resolveContractManifest finds workspace name and hash alias", () => {
  const manifest = {
    abi: { methods: [{ name: "GetNumber", parameters: [] }], events: [] },
  } as unknown as AutoCompleteData["contractManifests"][string];
  const autoComplete = data({
    contractManifests: {
      contracttestContract: manifest,
      "0xabc": manifest,
    },
    contractNames: { "0xabc": "contracttestContract" },
  });
  assert.equal(
    resolveContractManifest(autoComplete, "#contracttestContract"),
    manifest
  );
  assert.equal(resolveContractManifest(autoComplete, "#0xabc"), manifest);
});

test("manifestMethods hides system methods", () => {
  const methods = manifestMethods({
    abi: {
      methods: [
        { name: "_deploy", parameters: [] },
        { name: "GetNumber", parameters: [] },
      ],
    },
  });
  assert.deepEqual(
    methods.map((m) => m.name),
    ["GetNumber"]
  );
});

test("resolveContractManifest finds a NEP-17 workspace contract by name", () => {
  const manifest = {
    abi: {
      methods: [
        { name: "_deploy", parameters: [] },
        { name: "symbol", parameters: [] },
        { name: "decimals", parameters: [] },
        { name: "transfer", parameters: [{ name: "to", type: "Hash160" }] },
        { name: "Mint", parameters: [{ name: "to", type: "Hash160" }] },
      ],
      events: [],
    },
  } as unknown as AutoCompleteData["contractManifests"][string];
  const autoComplete = data({
    contractManifests: { Nep17Contract: manifest },
    contractPaths: {
      Nep17Contract: ["/workspace/contracts/Nep17/src/bin/sc/Nep17Contract.nef"],
    },
  });
  assert.equal(resolveContractManifest(autoComplete, "#Nep17Contract"), manifest);
  assert.equal(resolveContractManifest(autoComplete, "nep17contract"), manifest);
  assert.deepEqual(
    manifestMethods(manifest).map((method) => method.name),
    ["symbol", "decimals", "transfer", "Mint"]
  );
});

test("resolveContractManifest finds a contract from its .nef path", () => {
  const manifest = {
    abi: { methods: [{ name: "GetResponse", parameters: [] }], events: [] },
  } as unknown as AutoCompleteData["contractManifests"][string];
  const autoComplete = data({
    contractManifests: { OracleRequest: manifest },
    contractPaths: {
      OracleRequest: [
        "/workspace/contracts/Oracle/src/bin/sc/OracleRequest.nef",
      ],
    },
  });
  assert.equal(
    resolveContractManifest(autoComplete, "OracleRequest"),
    manifest
  );
});
