import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

test("accountChoices reads wallets from the selected Express identifier plus workspace NEP-6 files", () => {
  const source = readFileSync(join(__dirname, "neoExpressCommands.ts"), "utf8");
  assert.match(
    source,
    /export async function accountChoices\(\s*identifier: BlockchainIdentifier,\s*autoComplete\?: AutoComplete\s*\)/
  );
  assert.match(source, /identifier\.getWalletAddresses\(\)/);
  assert.match(source, /buildAccountChoices/);
  assert.match(source, /signerForAccountChoice/);
  assert.match(source, /autoComplete\?\.data\.wellKnownAddresses/);
});

test("ensureAccountHasGas does not look up addresses on the active connection", () => {
  const txPrep = readFileSync(join(__dirname, "../util/txPrep.ts"), "utf8");
  assert.match(txPrep, /resolveAccountForIdentifier/);
  assert.match(txPrep, /identifier\.getWalletAddresses\(\)/);
  assert.doesNotMatch(txPrep, /wellKnownAddresses/);
});
