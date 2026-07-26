import assert from "node:assert/strict";
import { describe, it } from "node:test";

import * as neonSc from "@cityofzion/neon-core/lib/sc";

import AutoCompleteData from "./autoCompleteData";
import indexContractState from "./indexContractState";

function createIndex(): AutoCompleteData {
  return {
    addressNames: {},
    contractManifests: {},
    contractNames: {},
    contractPaths: {},
    wellKnownAddresses: {},
  };
}

describe("indexContractState", () => {
  it("indexes an Express contract state by hash and manifest name", () => {
    const index = createIndex();
    const manifest = {
      name: "SampleContract",
      abi: {
        methods: [
          {
            name: "changeNumber",
            parameters: [{ name: "positiveNumber", type: "Integer" }],
            returntype: "Boolean",
            offset: 0,
            safe: false,
          },
        ],
        events: [],
      },
    } as Partial<neonSc.ContractManifestJson>;

    assert.equal(
      indexContractState(index, {
        hash: "0xbff7777ee411e21215ab73f428149d26f4efdfd0",
        manifest,
      }),
      true
    );
    assert.equal(index.contractManifests.SampleContract, manifest);
    assert.equal(
      index.contractManifests[
        "0xbff7777ee411e21215ab73f428149d26f4efdfd0"
      ],
      manifest
    );
    assert.equal(
      index.contractNames[
        "0xbff7777ee411e21215ab73f428149d26f4efdfd0"
      ],
      "SampleContract"
    );
  });

  it("uses the requested name when the manifest has no name", () => {
    const index = createIndex();
    const manifest = {
      abi: { methods: [], events: [] },
    } as Partial<neonSc.ContractManifestJson>;

    assert.equal(
      indexContractState(
        index,
        { hash: "0x01", manifest },
        "FallbackContract"
      ),
      true
    );
    assert.equal(index.contractManifests.FallbackContract, manifest);
    assert.equal(index.contractNames["0x01"], "FallbackContract");
  });

  it("does not index incomplete contract states", () => {
    const index = createIndex();

    assert.equal(indexContractState(index, { hash: "0x01" }), false);
    assert.equal(indexContractState(index, { manifest: {} }), false);
    assert.deepEqual(index.contractManifests, {});
    assert.deepEqual(index.contractNames, {});
  });
});
