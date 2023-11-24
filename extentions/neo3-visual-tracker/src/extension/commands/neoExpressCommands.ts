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
import StorageExplorerPanelController from "../panelControllers/storageExplorerPanelController";
import TrackerPanelController from "../panelControllers/trackerPanelController";
import workspaceFolder from "../util/workspaceFolder";

export default class NeoExpressCommands {
  static async contractDeploy(
    neoExpress: NeoExpress,
    contractDetector: ContractDetector,
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    commandArguments?: CommandArguments
  ) {
    const identifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express"));
    if (!identifier) {
      return;
    }
    if (!Object.keys(contractDetector.contracts).length) {
      vscode.window.showErrorMessage(
        "No compiled contracts were found in the current workspace. A compiled contract (*.nef file) along with its manifest (*.manifest.json file) is required for deployment."
      );
      return;
    }
    const walletNames = Object.keys(await identifier.getWalletAddresses());
    const account = await IoHelpers.multipleChoice(
      "Select an account...",
      ...walletNames
    );
    if (!account) {
      return;
    }
    const contractFile =
      commandArguments?.path ||
      (await IoHelpers.multipleChoiceFiles(
        `Use account "${account}" to deploy...`,
        ...Object.values(contractDetector.contracts).map(
          (_) => _.absolutePathToNef
        )
      ));
    if (!contractFile) {
      return;
    }
    const output = await neoExpress.run(
      "contract",
      "deploy",
      contractFile,
      account,
      "-i",
      identifier.configPath
    );
    NeoExpressCommands.showResult(output);
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
    const worksapcePath = (vscode.workspace.workspaceFolders || [])[0]?.uri
      .fsPath;
    const configSavePath = await IoHelpers.pickSaveFile(
      "Create",
      "Neo Express Configurations",
      "neo-express",
      worksapcePath
        ? vscode.Uri.file(posixPath(worksapcePath, "default.neo-express"))
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

  static async reset(
    neoExpress: NeoExpress,
    neoExpressInstanceManager: NeoExpressInstanceManager,
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    commandArguments?: CommandArguments
  ) {
    const blockchainIdentifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express"));
    if (!blockchainIdentifier) {
      return;
    }
    const confirmed = await IoHelpers.yesNo(
      `Are you sure that you want to reset "${blockchainIdentifier.configPath}"?`
    );
    if (!confirmed) {
      return;
    }
    const wasRunning =
      neoExpressInstanceManager.runningInstance?.configPath ===
      blockchainIdentifier.configPath;
    if (wasRunning) {
      await neoExpressInstanceManager.stopAll();
    }
    try {
      const output = await neoExpress.run(
        "reset",
        "-f",
        "-i",
        blockchainIdentifier.configPath
      );
      NeoExpressCommands.showResult(output);
    } finally {
      if (wasRunning) {
        await neoExpressInstanceManager.run(blockchainsTreeDataProvider, {
          blockchainIdentifier,
        });
      }
    }
  }

  static async restoreCheckpoint(
    neoExpress: NeoExpress,
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
    const output = await neoExpress.run(
      "checkpoint",
      "restore",
      "-f",
      "-i",
      identifier.configPath,
      filename
    );
    NeoExpressCommands.showResult(output);
  }

  static async transfer(
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
    let asset: string | undefined = undefined;
    if (commandArguments?.asset?.toUpperCase() === "NEO") {
      asset = "NEO";
    } else if (commandArguments?.asset?.toUpperCase() === "GAS") {
      asset = "GAS";
    } else {
      asset = await IoHelpers.multipleChoice("Select an asset", "NEO", "GAS");
    }
    if (!asset) {
      return;
    }
    const amount =
      commandArguments?.amount === undefined
        ? await IoHelpers.enterNumber(
            `How many ${asset} should be transferred?`
          )
        : commandArguments.amount;
    if (amount === undefined) {
      return;
    }
    const walletNames = Object.keys(await identifier.getWalletAddresses());
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
      receiver = await IoHelpers.enterString("Enter the recipients address");
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
      sender,
      receiver
    );
    NeoExpressCommands.showResult(output);
  }

  static async walletCreate(
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
  }

  private static showResult(output: { message: string; isError?: boolean }) {
    if (output.isError) {
      vscode.window.showErrorMessage(output.message || "Unknown error");
    } else {
      vscode.window.showInformationMessage(
        output.message || "Command succeeded"
      );
    }
  }
}
