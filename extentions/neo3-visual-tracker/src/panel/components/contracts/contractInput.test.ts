import assert from "node:assert/strict";
import test from "node:test";

import { listContractOptions } from "./ContractInput";
import AutoCompleteData from "../../../shared/autoCompleteData";

function data(
  manifests: AutoCompleteData["contractManifests"]
): AutoCompleteData {
  return {
    contractManifests: manifests,
    contractNames: {},
    contractPaths: {},
    wellKnownAddresses: {},
    addressNames: {},
  };
}

test("listContractOptions uses #names and skips hashes", () => {
  assert.deepEqual(
    listContractOptions(
      data({
        Nep17Contract: { abi: { methods: [], events: [] } },
        "0xabc": { abi: { methods: [], events: [] } },
        OracleRequest: { abi: { methods: [], events: [] } },
      })
    ),
    ["#Nep17Contract", "#OracleRequest"]
  );
});
