import * as fs from "fs";
import * as vscode from "vscode";

import AutoComplete from "../autoComplete";
import BlockchainIdentifier from "../blockchainIdentifier";
import BlockchainMonitorPool from "../blockchainMonitor/blockchainMonitorPool";
import BlockchainsTreeDataProvider from "../vscodeProviders/blockchainsTreeDataProvider";
import CheckpointDetector from "../fileDetectors/checkpointDetector";
import { CommandArguments } from "./commandArguments";
import ContractDetector from "../fileDetectors/contractDetector";
import IoHelpers from "../util/ioHelpers";
import NeoExpress from "../neoExpress/neoExpress";
import NeoExpressInstanceManager from "../neoExpress/neoExpressInstanceManager";
import posixPath from "../util/posixPath";
import stripAnsi from "../util/stripAnsi";
import {
  AccountChoice,
  addressForAccountChoice,
  buildAccountChoices,
  contractsWithStandard,
  signerForAccountChoice,
} from "../../shared/expressWalletAddresses";
import { isFailedTx, isNodeStillRunningError, parseSubmittedTxids } from "../../shared/neoExpressTx";
import { ensureAccountHasGas, passwordFlags, waitForContract, waitForTransactionResult, withStatus } from "../util/txPrep";
import StorageExplorerPanelController from "../panelControllers/storageExplorerPanelController";
import TrackerPanelController from "../panelControllers/trackerPanelController";
import workspaceFolder from "../util/workspaceFolder";

export async function accountChoices(
  identifier: BlockchainIdentifier,
  autoComplete?: AutoComplete
): Promise<AccountChoice[]> {
  return buildAccountChoices(
    await identifier.getWalletAddresses(),
    autoComplete?.data.accountSigners,
    autoComplete?.data.wellKnownAddresses
  );
}

function accountSigner(label: string, choices: AccountChoice[]): string {
  return signerForAccountChoice(label, choices);
}

function accountAddress(label: string, choices: AccountChoice[]): string {
  return addressForAccountChoice(label, choices);
}

function choiceLabels(choices: AccountChoice[]): string[] {
  return choices.map((choice) => choice.label);
}

export default class NeoExpressCommands {
  static async contractDeploy(
    neoExpress: NeoExpress,
    contractDetector: ContractDetector,
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    commandArguments?: CommandArguments,
    autoComplete?: AutoComplete,
    preferred?: BlockchainIdentifier
  ) {
    const identifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express", preferred));
    if (!identifier) {
      return;
    }
    if (!Object.keys(contractDetector.contracts).length) {
      vscode.window.showErrorMessage(
        "No compiled contracts were found in the current workspace. A compiled contract (*.nef file) along with its manifest (*.manifest.json file) is required for deployment."
      );
      return;
    }
    const choices = await accountChoices(identifier, autoComplete);
    const accountNames = choiceLabels(choices);
    let accountName = commandArguments?.sender;
    if (!accountName || accountNames.indexOf(accountName) === -1) {
      accountName = await IoHelpers.multipleChoice(
        "Select an account (genesis holds NEO/GAS)...",
        ...accountNames
      );
    }
    if (!accountName) {
      return;
    }
    const account = accountSigner(accountName, choices);
    const contractFile =
      commandArguments?.path ||
      (await IoHelpers.multipleChoiceFiles(
        `Use account "${accountName}" to deploy...`,
        ...Object.values(contractDetector.contracts).map(
          (_) => _.absolutePathToNef
        )
      ));
    if (!contractFile) {
      return;
    }
    const password = await passwordFlags(account);
    if (!password) {
      return;
    }
    await withStatus("Deploying contract", async (report) => {
      report("Checking GAS balance...");
      if (
        !(await ensureAccountHasGas(
          neoExpress,
          identifier,
          accountName,
          account,
          report,
          accountAddress(accountName, choices)
        ))
      ) {
        return;
      }
      const contractName =
        contractDetector.contracts[
          Object.keys(contractDetector.contracts).find(
            (name) =>
              contractDetector.contracts[name].absolutePathToNef ===
              posixPath(contractFile)
          ) || ""
        ]?.manifest?.name ||
        posixPath(contractFile).split("/").pop()?.replace(/\.nef$/i, "") ||
        "";
      report("Submitting deploy transaction...");
      const output = await NeoExpressCommands.runDeploy(
        neoExpress,
        identifier,
        contractFile,
        account,
        password,
        !!commandArguments?.force,
        report,
        contractName
      );
      if (!output) {
        return;
      }
      await contractDetector.refresh();
      vscode.window.showInformationMessage(
        stripAnsi(output) || `Deployed ${contractName}.`
      );
    });
  }

  static async create(
    context: vscode.ExtensionContext,
    neoExpress: NeoExpress,
    neoExpressInstanceManager: NeoExpressInstanceManager,
    autoComplete: AutoComplete,
    blockchainMonitorPool: BlockchainMonitorPool,
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider
  ) {
    const nodeCount = await IoHelpers.multipleChoice(
      "Number of nodes in the new instance",
      "1",
      "4",
      "7"
    );
    if (!nodeCount) {
      return;
    }
    const workspacePath = (vscode.workspace.workspaceFolders || [])[0]?.uri
      .fsPath;
    const configSavePath = await IoHelpers.pickSaveFile(
      "Create",
      "Neo Express Configurations",
      "neo-express",
      workspacePath
        ? vscode.Uri.file(posixPath(workspacePath, "default.neo-express"))
        : undefined
    );
    if (!configSavePath) {
      return;
    }
    const output = await neoExpress.run(
      "create",
      "-f",
      "-c",
      nodeCount,
      "-o",
      configSavePath
    );
    NeoExpressCommands.showResult(output);
    if (!output.isError) {
      const blockchainIdentifier =
        await BlockchainIdentifier.fromNeoExpressConfig(
          context.extensionPath,
          configSavePath
        );
      if (blockchainIdentifier) {
        await neoExpressInstanceManager.run(blockchainsTreeDataProvider, {
          blockchainIdentifier,
        });
        const rpcUrl = await blockchainIdentifier.selectRpcUrl();
        if (rpcUrl) {
          new TrackerPanelController(
            context,
            rpcUrl,
            autoComplete,
            blockchainMonitorPool
          );
        }
      }
    }
  }

  static async createCheckpoint(
    neoExpress: NeoExpress,
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    commandArguments?: CommandArguments
  ) {
    const identifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express"));
    if (!identifier) {
      return;
    }
    if (!(await identifier.isSingleNodeExpress())) {
      vscode.window.showErrorMessage(
        "Checkpoints can only be created for single-node Neo Express instances."
      );
      return;
    }
    const rootFolder = workspaceFolder();
    if (!rootFolder) {
      vscode.window.showErrorMessage(
        "Please open a folder in your Visual Studio Code workspace before creating checkpoints"
      );
      return;
    }
    const checkpointsFolder = posixPath(rootFolder, "checkpoints");
    try {
      await fs.promises.mkdir(checkpointsFolder);
    } catch {}
    let filename = posixPath(checkpointsFolder, "checkpoint-1");
    let i = 1;
    while (fs.existsSync(`${filename}.neoxp-checkpoint`)) {
      i++;
      filename = posixPath(checkpointsFolder, `checkpoint-${i}`);
    }
    const output = await neoExpress.run(
      "checkpoint",
      "create",
      "-i",
      identifier.configPath,
      filename
    );
    NeoExpressCommands.showResult(output);
  }

  static async customCommand(
    neoExpress: NeoExpress,
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    commandArguments?: CommandArguments
  ) {
    const identifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express"));
    if (!identifier) {
      return;
    }
    const command = await IoHelpers.enterString("Enter a neo-express command");
    if (!command) {
      return;
    }
    const output = await neoExpress.runUnsafe(
      undefined,
      command,
      "-i",
      identifier.configPath
    );
    NeoExpressCommands.showResult(output);
  }

  static async exploreStorage(
    context: vscode.ExtensionContext,
    autoComplete: AutoComplete,
    blockchainMonitorPool: BlockchainMonitorPool,
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    neoExpress: NeoExpress,
    commandArguments?: CommandArguments
  ) {
    const identifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express"));
    if (!identifier) {
      return;
    }
    new StorageExplorerPanelController(
      context,
      identifier,
      autoComplete,
      blockchainMonitorPool,
      await identifier.selectRpcUrl(),
      neoExpress
    );
  }

  private static async runDeploy(
    neoExpress: NeoExpress,
    identifier: BlockchainIdentifier,
    contractFile: string,
    account: string,
    password: string[],
    force: boolean,
    report: (msg: string) => void,
    contractName?: string
  ): Promise<string | null> {
    const gasFlags = (await neoExpress.supportsDeployGasOption())
      ? (["-g", "1"] as const)
      : [];
    let output = await neoExpress.run(
      "contract",
      "deploy",
      contractFile,
      account,
      ...gasFlags,
      "-i",
      identifier.configPath,
      ...(force ? ["-f"] : []),
      ...password
    );
    if (
      output.isError &&
      gasFlags.length &&
      /unrecognized option\s+'?-g/i.test(stripAnsi(output.message))
    ) {
      output = await neoExpress.run(
        "contract",
        "deploy",
        contractFile,
        account,
        "-i",
        identifier.configPath,
        ...(force ? ["-f"] : []),
        ...password
      );
    }
    const confirmed = await NeoExpressCommands.confirmSubmittedTx(
      neoExpress,
      identifier,
      output,
      report
    );
    if (!confirmed) {
      return null;
    }
    if (!parseSubmittedTxids(confirmed)[0] && contractName) {
      const onChain = await waitForContract(
        neoExpress,
        identifier,
        contractName,
        12000,
        report
      );
      if (!onChain) {
        vscode.window.showErrorMessage(
          confirmed +
            " The transaction was submitted, but the contract is not on chain."
        );
        return null;
      }
    }
    return confirmed;
  }

  private static async confirmSubmittedTx(
    neoExpress: NeoExpress,
    identifier: BlockchainIdentifier,
    output: { message: string; isError?: boolean },
    report: (msg: string) => void
  ): Promise<string | null> {
    if (output.isError || isFailedTx(output.message)) {
      NeoExpressCommands.showResult(output);
      return null;
    }
    const txid = parseSubmittedTxids(output.message)[0];
    if (txid) {
      const confirmed = await waitForTransactionResult(
        neoExpress,
        identifier,
        txid,
        20000,
        report
      );
      if (!confirmed.ok) {
        vscode.window.showErrorMessage(
          `Transaction failed.\n${confirmed.message}`
        );
        return null;
      }
    }
    return stripAnsi(output.message);
  }

  static async reset(
    neoExpress: NeoExpress,
    neoExpressInstanceManager: NeoExpressInstanceManager,
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    commandArguments?: CommandArguments,
    preferred?: BlockchainIdentifier
  ) {
    const blockchainIdentifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express", preferred));
    if (!blockchainIdentifier) {
      return;
    }
    const confirmed = await IoHelpers.yesNo(
      `Are you sure that you want to reset "${blockchainIdentifier.configPath}"?`
    );
    if (!confirmed) {
      return;
    }
    const wasRunning = neoExpressInstanceManager.isRunning(blockchainIdentifier);
    await withStatus("Resetting Neo Express", async (report) => {
      report("Stopping node...");
      await neoExpressInstanceManager.stopAll(blockchainIdentifier);
      report("Resetting chain data...");
      let output = await neoExpress.run(
        "reset",
        "-f",
        "--all",
        "-i",
        blockchainIdentifier.configPath
      );
      if (isNodeStillRunningError(output.message)) {
        report("Node still running; stopping again...");
        await neoExpressInstanceManager.stopAll(blockchainIdentifier);
        await new Promise((resolve) => setTimeout(resolve, 1000));
        output = await neoExpress.run(
          "reset",
          "-f",
          "--all",
          "-i",
          blockchainIdentifier.configPath
        );
      }
      NeoExpressCommands.showResult(output);
      if (output.isError || isFailedTx(output.message) || isNodeStillRunningError(output.message)) {
        return;
      }
      if (wasRunning) {
        report("Restarting node...");
        await neoExpressInstanceManager.run(blockchainsTreeDataProvider, {
          blockchainIdentifier,
        });
      }
    });
  }

  static async restoreCheckpoint(
    neoExpress: NeoExpress,
    neoExpressInstanceManager: NeoExpressInstanceManager,
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    checkpointDetector: CheckpointDetector,
    commandArguments?: CommandArguments
  ) {
    const identifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express"));
    if (!identifier) {
      return;
    }
    const filename = await IoHelpers.multipleChoiceFiles(
      "Select a checkpoint to restore",
      ...checkpointDetector.checkpointFiles
    );
    if (!filename) {
      return;
    }
    const confirmed = await IoHelpers.yesNo(
      `Are you sure that you want to restore "${identifier.configPath}" to the checkpoint "${filename}"?`
    );
    if (!confirmed) {
      return;
    }
    const wasRunning = neoExpressInstanceManager.isRunning(identifier);
    await neoExpressInstanceManager.stopAll(identifier);
    const output = await neoExpress.run(
      "checkpoint",
      "restore",
      "-f",
      "-i",
      identifier.configPath,
      filename
    );
    NeoExpressCommands.showResult(output);
    if (output.isError || isNodeStillRunningError(output.message)) {
      return;
    }
    if (wasRunning) {
      await neoExpressInstanceManager.run(blockchainsTreeDataProvider, {
        blockchainIdentifier: identifier,
      });
    }
  }

  static async transfer(
    neoExpress: NeoExpress,
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    autoComplete?: AutoComplete,
    commandArguments?: CommandArguments,
    preferred?: BlockchainIdentifier
  ) {
    const identifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express", preferred));
    if (!identifier) {
      return;
    }
    const nep17 = autoComplete
      ? contractsWithStandard(
          autoComplete.data.contractManifests,
          "NEP-17"
        )
      : [];
    const assets = ["NEO", "GAS", ...nep17];
    let asset: string | undefined = commandArguments?.asset;
    if (!asset || assets.indexOf(asset) === -1) {
      asset = await IoHelpers.multipleChoice(
        "Select an asset (NEO, GAS, or NEP-17)",
        ...assets
      );
    }
    if (!asset) {
      return;
    }
    const amount =
      commandArguments?.amount === undefined
        ? await IoHelpers.enterNumber(
            `How many ${asset} should be transferred? Use a whole number, or check the token decimals.`
          )
        : commandArguments.amount;
    if (amount === undefined) {
      return;
    }
    const choices = await accountChoices(identifier, autoComplete);
    const walletNames = choiceLabels(choices);
    let sender = commandArguments?.sender;
    if (!sender || walletNames.indexOf(sender) === -1) {
      sender = await IoHelpers.multipleChoice(
        `Transfer ${amount} ${asset} from which wallet?`,
        ...walletNames
      );
    }
    if (!sender) {
      return;
    }
    const senderArg = accountSigner(sender, choices);
    const senderPassword = await passwordFlags(senderArg);
    if (!senderPassword) {
      return;
    }
    if (
      !(await ensureAccountHasGas(
        neoExpress,
        identifier,
        sender,
        senderArg,
        undefined,
        accountAddress(sender, choices)
      ))
    ) {
      return;
    }
    let receiver = commandArguments?.receiver;
    const CUSTOM_ADDRESS = "(enter an address manually)";
    if (!receiver || walletNames.indexOf(receiver) === -1) {
      receiver = await IoHelpers.multipleChoice(
        `Transfer ${amount} ${asset} from '${sender}' to...`,
        ...walletNames,
        CUSTOM_ADDRESS
      );
    }
    if (receiver === CUSTOM_ADDRESS) {
      receiver = await IoHelpers.enterString("Enter the recipient address");
    }
    if (!receiver) {
      return;
    }
    const output = await neoExpress.run(
      "transfer",
      "-i",
      identifier.configPath,
      `${amount}`,
      asset,
      senderArg,
      accountAddress(receiver, choices),
      ...senderPassword
    );
    NeoExpressCommands.showResult(output);
  }

  static async transferNft(
    neoExpress: NeoExpress,
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    autoComplete?: AutoComplete,
    commandArguments?: CommandArguments,
    preferred?: BlockchainIdentifier
  ) {
    const identifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express", preferred));
    if (!identifier) {
      return;
    }
    const nep11 = autoComplete
      ? contractsWithStandard(
          autoComplete.data.contractManifests,
          "NEP-11"
        )
      : [];
    const contract =
      commandArguments?.asset ||
      (await IoHelpers.multipleChoice(
        "Select a NEP-11 contract",
        ...(nep11.length ? nep11 : ["(enter a symbol or hash)"])
      ));
    const nftContract =
      !contract || contract.startsWith("(")
        ? await IoHelpers.enterString("NEP-11 symbol or script hash")
        : contract;
    if (!nftContract) {
      return;
    }
    const tokenId = await IoHelpers.enterString(
      "Token id (0x-prefixed hex or base64)"
    );
    if (!tokenId) {
      return;
    }
    const choices = await accountChoices(identifier, autoComplete);
    const walletNames = choiceLabels(choices);
    const sender = await IoHelpers.multipleChoice(
      "Transfer NFT from which wallet?",
      ...walletNames
    );
    if (!sender) {
      return;
    }
    const senderArg = accountSigner(sender, choices);
    const senderPassword = await passwordFlags(senderArg);
    if (!senderPassword) {
      return;
    }
    if (
      !(await ensureAccountHasGas(
        neoExpress,
        identifier,
        sender,
        senderArg,
        undefined,
        accountAddress(sender, choices)
      ))
    ) {
      return;
    }
    const CUSTOM_ADDRESS = "(enter an address manually)";
    let receiver = await IoHelpers.multipleChoice(
      `Transfer NFT from '${sender}' to...`,
      ...walletNames,
      CUSTOM_ADDRESS
    );
    if (receiver === CUSTOM_ADDRESS) {
      receiver = await IoHelpers.enterString("Enter the recipient address");
    }
    if (!receiver) {
      return;
    }
    const output = await neoExpress.run(
      "transfernft",
      "-i",
      identifier.configPath,
      nftContract,
      tokenId,
      senderArg,
      accountAddress(receiver, choices),
      ...senderPassword
    );
    NeoExpressCommands.showResult(output);
  }

  static async walletCreate(
    neoExpress: NeoExpress,
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    commandArguments?: CommandArguments,
    preferred?: BlockchainIdentifier
  ) {
    const identifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express", preferred));
    if (!identifier) {
      return;
    }
    const walletName = await IoHelpers.enterString("Wallet name");
    if (!walletName) {
      return;
    }
    const output = await neoExpress.run(
      "wallet",
      "create",
      walletName,
      "-i",
      identifier.configPath
    );
    NeoExpressCommands.showResult(output);
    if (output.isError) {
      return;
    }
    const fund = await IoHelpers.yesNo(
      `Send 10000 GAS from genesis to "${walletName}" so it can pay fees?`
    );
    if (!fund) {
      return;
    }
    const fundOutput = await neoExpress.run(
      "transfer",
      "-i",
      identifier.configPath,
      "10000",
      "gas",
      "genesis",
      walletName
    );
    NeoExpressCommands.showResult(fundOutput);
  }

  private static showResult(output: { message: string; isError?: boolean }) {
    const message = stripAnsi(output.message || "");
    if (output.isError || isFailedTx(message) || isNodeStillRunningError(message)) {
      vscode.window.showErrorMessage(
        isNodeStillRunningError(message)
          ? "Could not reset or start Neo Express because a node is still running. Close the Neo Express terminal and try again."
          : message || "Unknown error"
      );
    } else {
      vscode.window.showInformationMessage(message || "Command succeeded");
    }
  }
}
