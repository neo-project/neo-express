import assert from "node:assert/strict";
import test from "node:test";

import {
  formatInvokeMessage,
  isContractOnChain,
  isFailedTx,
  isNodeStillRunningError,
  looksLikeEncryptedWallet,
  parseApplicationLogState,
  parseGasBalance,
  parseSubmittedTxids,
} from "./neoExpressTx";

test("looksLikeEncryptedWallet detects NEP-6 files", () => {
  assert.equal(
    looksLikeEncryptedWallet("/workspace/wallets/alice.neo-wallet.json"),
    true
  );
  assert.equal(looksLikeEncryptedWallet("genesis"), false);
  assert.equal(looksLikeEncryptedWallet("node1"), false);
});

test("parseGasBalance reads GAS from show balances output", () => {
  assert.equal(
    parseGasBalance(
      "GAS (0xd2a4cff31913016155e38e474a2c06d08be276cf)\n  balance: 19.999"
    ),
    19.999
  );
  assert.equal(parseGasBalance("No balances for alice"), 0);
  assert.equal(parseGasBalance("NEO\n  balance: 100"), null);
});

test("isFailedTx detects FAULT and insufficient GAS", () => {
  assert.equal(isFailedTx('Tx FAULT: hash=0xabc exception="Insufficient GAS."'), true);
  assert.equal(isFailedTx("Deployment of Token (0xabc) Transaction 0xdef submitted"), false);
  assert.equal(
    isFailedTx('Tx FAULT: exception="Contract Already Exists: 0xabc"'),
    true
  );
});

test("isNodeStillRunningError detects mutex / already-running CLI errors", () => {
  assert.equal(
    isNodeStillRunningError(
      "System.InvalidOperationException: node NSkaz8cyADPr6w4Ff97rDYY1TabqEBmFTs currently running"
    ),
    true
  );
  assert.equal(isNodeStillRunningError("System.Exception: Node already running"), true);
  assert.equal(isNodeStillRunningError("node 0 reset"), false);
});

test("isContractOnChain treats empty get output as not deployed", () => {
  assert.equal(isContractOnChain("[]"), false);
  assert.equal(isContractOnChain(""), false);
  assert.equal(
    isContractOnChain('[\n  {\n    "name": "Nep17Contract",\n    "hash": "0xabc"\n  }\n]'),
    true
  );
});

test("parseApplicationLogState reads vmstate from show transaction JSON", () => {
  assert.equal(
    parseApplicationLogState('{ "executions": [ { "vmstate": "FAULT" } ] }'),
    "FAULT"
  );
  assert.equal(
    parseApplicationLogState('{ "executions": [ { "vmstate": "HALT" } ] }'),
    "HALT"
  );
  assert.equal(parseApplicationLogState("not json"), null);
});

test("parseSubmittedTxids extracts 32-byte transaction hashes", () => {
  assert.deepEqual(
    parseSubmittedTxids(
      "Invocation Transaction 0xea42778535dd152aa7eca518a7c323ac98b11a74538af03e509c3684caa3db07 submitted"
    ),
    ["0xea42778535dd152aa7eca518a7c323ac98b11a74538af03e509c3684caa3db07"]
  );
  assert.deepEqual(parseSubmittedTxids("contract hash 0xd95198ef802d354260685d385a1579e0858e9239"), []);
});

test("formatInvokeMessage pretty-prints --results JSON", () => {
  assert.equal(
    formatInvokeMessage(
      '{"state":"HALT","gasconsumed":"984060","exception":null,"stack":[{"type":"ByteString","value":"abc"}]}'
    ),
    [
      "VM State: HALT",
      "Gas Consumed: 984060",
      "Result Stack:",
      '[\n  {\n    "type": "ByteString",\n    "value": "abc"\n  }\n]',
    ].join("\n")
  );
  assert.equal(
    formatInvokeMessage("Invocation Transaction 0xabc submitted"),
    "Invocation Transaction 0xabc submitted"
  );
});
