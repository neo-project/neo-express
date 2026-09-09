import * as path from "path";
import * as vscode from "vscode";

import BlockchainIdentifier from "../blockchainIdentifier";
import IoHelpers from "./ioHelpers";
import NeoExpress from "../neoExpress/neoExpress";
import stripAnsi from "./stripAnsi";
import { resolveAccountForIdentifier } from "../../shared/expressWalletAddresses";
import {
  isContractOnChain,
  isFailedTx,
  looksLikeEncryptedWallet,
  parseApplicationLogState,
  parseGasBalance,
  parseSubmittedTxids,
} from "../../shared/neoExpressTx";

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export async function withStatus<T>(
  message: string,
  work: (report: (msg: string) => void) => Promise<T>
): Promise<T> {
  return vscode.window.withProgress(
    {
      location: vscode.ProgressLocation.Window,
      title: "Neo Express",
    },
    async (progress) => {
      progress.report({ message });
      return work((msg) => {
        progress.report({ message: msg });
      });
    }
  );
}

export async function passwordFlags(
  account: string
): Promise<string[] | undefined> {
  if (!looksLikeEncryptedWallet(account)) {
    return [];
  }
  const password = await IoHelpers.enterPassword(
    `Password for ${path.basename(account)}`
  );
  if (password === undefined) {
    return undefined;
  }
  return ["--password", password];
}

export async function ensureAccountHasGas(
  neoExpress: NeoExpress,
  identifier: BlockchainIdentifier,
  displayName: string,
  signer: string,
  report?: (msg: string) => void,
  address?: string
): Promise<boolean> {
  if (displayName === "genesis" || signer === "genesis") {
    return true;
  }
  const lookup = address || resolveAccountForIdentifier(
    displayName,
    signer,
    await identifier.getWalletAddresses()
  );
  report?.(`Checking GAS for ${displayName}...`);
  const existing = await readGasBalance(neoExpress, identifier, lookup);
  if (existing === null || existing > 0) {
    return true;
  }
  const fund = await IoHelpers.yesNo(
    `"${displayName}" has no GAS. Send 10000 GAS from genesis so this account can pay fees?`
  );
  if (!fund) {
    return false;
  }
  report?.(`Sending 10000 GAS from genesis to ${displayName}...`);
  const fundOutput = await neoExpress.run(
    "transfer",
    "-i",
    identifier.configPath,
    "10000",
    "gas",
    "genesis",
    lookup
  );
  if (fundOutput.isError || isFailedTx(fundOutput.message)) {
    vscode.window.showErrorMessage(
      stripAnsi(fundOutput.message) || "Could not send GAS from genesis."
    );
    return false;
  }
  const confirmed = await waitForGasBalance(
    neoExpress,
    identifier,
    lookup,
    1,
    45000,
    report
  );
  if (confirmed === null || confirmed <= 0) {
    vscode.window.showErrorMessage(
      `Sent GAS from genesis to ${displayName}, but the balance is still 0. Wait for the next block and try again.`
    );
    return false;
  }
  vscode.window.showInformationMessage(
    `Sent 10000 GAS from genesis to ${displayName}. Balance is now ${confirmed} GAS.`
  );
  return true;
}

async function readGasBalance(
  neoExpress: NeoExpress,
  identifier: BlockchainIdentifier,
  account: string
): Promise<number | null> {
  const output = await neoExpress.run(
    "show",
    "balances",
    account,
    "-i",
    identifier.configPath
  );
  return parseGasBalance(output.message);
}

export async function waitForGasBalance(
  neoExpress: NeoExpress,
  identifier: BlockchainIdentifier,
  account: string,
  minGas: number,
  timeoutMs = 45000,
  report?: (msg: string) => void
): Promise<number | null> {
  const started = Date.now();
  let last: number | null = null;
  while (Date.now() - started < timeoutMs) {
    report?.(`Waiting for GAS on ${account}...`);
    last = await readGasBalance(neoExpress, identifier, account);
    if (last !== null && last >= minGas) {
      return last;
    }
    await sleep(1000);
  }
  return last;
}

export async function waitForContract(
  neoExpress: NeoExpress,
  identifier: BlockchainIdentifier,
  contractName: string,
  timeoutMs = 45000,
  report?: (msg: string) => void
): Promise<boolean> {
  const started = Date.now();
  while (Date.now() - started < timeoutMs) {
    report?.(`Waiting for ${contractName} on chain...`);
    const output = await neoExpress.run(
      "contract",
      "get",
      contractName,
      "-i",
      identifier.configPath
    );
    if (!output.isError && isContractOnChain(output.message)) {
      return true;
    }
    await sleep(1000);
  }
  return false;
}

export async function waitForTransactionResult(
  neoExpress: NeoExpress,
  identifier: BlockchainIdentifier,
  txid: string,
  timeoutMs = 20000,
  report?: (msg: string) => void
): Promise<{ ok: boolean; message: string }> {
  const started = Date.now();
  let last = "";
  while (Date.now() - started < timeoutMs) {
    report?.(`Waiting for transaction ${txid.slice(0, 10)}...`);
    const output = await neoExpress.run(
      "show",
      "transaction",
      txid,
      "-i",
      identifier.configPath
    );
    last = stripAnsi(output.message);
    if (!output.isError) {
      const state = parseApplicationLogState(last);
      if (state === "FAULT" || isFailedTx(last)) {
        return { ok: false, message: last };
      }
      if (state === "HALT") {
        return { ok: true, message: last };
      }
    }
    await sleep(500);
  }
  return {
    ok: false,
    message: last || `Transaction ${txid} did not confirm`,
  };
}

export { isFailedTx, parseSubmittedTxids };
