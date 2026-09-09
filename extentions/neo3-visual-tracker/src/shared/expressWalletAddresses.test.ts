import assert from "node:assert/strict";
import test from "node:test";

import {
  addressForAccountChoice,
  buildAccountChoices,
  contractsWithStandard,
  isHiddenExpressConfig,
  parseExpressWalletAddresses,
  resolveAccountForIdentifier,
  signerForAccountChoice,
  workspaceNep6AccountNames,
  workspaceWalletDisplayName,
} from "./expressWalletAddresses";

test("parseExpressWalletAddresses includes genesis, node wallets, and user wallets", () => {
  const addresses = parseExpressWalletAddresses({
    wallets: [
      {
        name: "alice",
        accounts: [
          {
            "script-hash": "Nalice",
            "is-default": true,
            label: null,
          },
        ],
      },
    ],
    "consensus-nodes": [
      {
        wallet: {
          name: "node1",
          accounts: [
            {
              "script-hash": "Nnode1",
              "is-default": true,
              label: null,
            },
            {
              "script-hash": "Ngenesis",
              "is-default": false,
              label: "Consensus MultiSigContract",
            },
          ],
        },
      },
    ],
  });
  assert.deepEqual(addresses, {
    alice: "Nalice",
    node1: "Nnode1",
    genesis: "Ngenesis",
  });
});

test("workspaceWalletDisplayName uses the file name for Default account labels", () => {
  assert.equal(
    workspaceWalletDisplayName(
      "/workspace/wallets/alice.neo-wallet.json",
      "Default account"
    ),
    "alice"
  );
  assert.equal(
    workspaceWalletDisplayName("/wallets/ops.json", "Cold storage"),
    "Cold storage"
  );
});

test("hides nested test neo-express files from the blockchain list", () => {
  assert.equal(
    isHiddenExpressConfig(
      "/workspace/contracts/SampleToken/test/default.neo-express"
    ),
    true
  );
  assert.equal(
    isHiddenExpressConfig("/workspace/default.neo-express"),
    false
  );
});

test("workspaceNep6AccountNames keeps file-backed wallets, not Express names", () => {
  assert.deepEqual(
    workspaceNep6AccountNames({
      genesis: "genesis",
      owner: "owner",
      alice: "/workspace/alice.json",
    }).sort(),
    ["alice"]
  );
});

test("buildAccountChoices disambiguates Express owner from workspace owner.json", () => {
  const choices = buildAccountChoices(
    { genesis: "Ng", owner: "NexpressOwner" },
    {
      genesis: "genesis",
      owner: "/workspace/wallets/owner.json",
      alice: "/workspace/alice.json",
    }
  );
  assert.deepEqual(
    choices.map((choice) => choice.label),
    ["genesis", "owner", "alice", "owner (owner.json)"]
  );
  assert.equal(signerForAccountChoice("owner", choices), "owner");
  assert.equal(
    signerForAccountChoice("owner (owner.json)", choices),
    "/workspace/wallets/owner.json"
  );
  assert.equal(
    resolveAccountForIdentifier("owner", "owner", {
      genesis: "Ng",
      owner: "NexpressOwner",
    }),
    "NexpressOwner"
  );
  assert.equal(
    resolveAccountForIdentifier(
      "owner (owner.json)",
      "/workspace/wallets/owner.json",
      { genesis: "Ng", owner: "NexpressOwner" }
    ),
    "/workspace/wallets/owner.json"
  );
});

test("account choices keep workspace address separate from signer path", () => {
  const choices = buildAccountChoices(
    { genesis: "Ngenesis" },
    { alice: "/workspace/alice.json" },
    { alice: "Nalice" }
  );

  assert.equal(signerForAccountChoice("alice", choices), "/workspace/alice.json");
  assert.equal(addressForAccountChoice("alice", choices), "Nalice");
});

test("resolveAccountForIdentifier uses the selected Express config, not the active chain", () => {
  const chainB = { genesis: "NbGenesis", owner: "NbOwner" };
  assert.equal(
    resolveAccountForIdentifier("owner", "owner", chainB),
    "NbOwner"
  );
  assert.equal(
    resolveAccountForIdentifier("alice", "/workspace/alice.json", chainB),
    "/workspace/alice.json"
  );
  assert.notEqual(
    resolveAccountForIdentifier("owner", "owner", chainB),
    "NaOwner"
  );
});

test("contractsWithStandard lists NEP-17 names and skips hashes", () => {
  assert.deepEqual(
    contractsWithStandard(
      {
        Nep17Contract: { supportedstandards: ["NEP-17"] },
        "0xabc": { supportedstandards: ["NEP-17"] },
        Nep11Contract: { supportedstandards: ["NEP-11"] },
      },
      "NEP-17"
    ),
    ["Nep17Contract"]
  );
});
