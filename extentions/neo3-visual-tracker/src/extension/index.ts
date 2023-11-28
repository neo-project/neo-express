import * as vscode from "vscode";

import ActiveConnection from "./activeConnection";
import AutoComplete from "./autoComplete";
import BlockchainIdentifier from "./blockchainIdentifier";
import BlockchainMonitorPool from "./blockchainMonitor/blockchainMonitorPool";
import BlockchainsTreeDataProvider from "./vscodeProviders/blockchainsTreeDataProvider";
import CheckpointDetector from "./fileDetectors/checkpointDetector";
import {
  CommandArguments,
  sanitizeCommandArguments,
} from "./commands/commandArguments";
import ContractDetector from "./fileDetectors/contractDetector";
import ContractsTreeDataProvider from "./vscodeProviders/contractsTreeDataProvider";
import Log from "./util/log";
import NeoCommands from "./commands/neoCommands";
import NeoExpress from "./neoExpress/neoExpress";
import NeoExpressCommands from "./commands/neoExpressCommands";
import NeoExpressDetector from "./fileDetectors/neoExpressDetector";
import NeoExpressInstanceManager from "./neoExpress/neoExpressInstanceManager";
import NeoInvokeFileEditorProvider from "./vscodeProviders/neoInvokeFileEditorProvider";
import QuickStartViewProvider from "./vscodeProviders/quickStartViewProvider";
import ServerListDetector from "./fileDetectors/serverListDetector";
import Templates from "./templates/templates";
import TrackerCommands from "./commands/trackerCommands";
import WalletDetector from "./fileDetectors/walletDetector";
import WalletsTreeDataProvider from "./vscodeProviders/walletsTreeDataProvider";

const LOG_PREFIX = "index";

function registerCommand(
  context: vscode.ExtensionContext,
  commandId: string,
  handler: (commandArguments: CommandArguments) => Promise<void>
) {
  context.subscriptions.push(
    vscode.commands.registerCommand(
      commandId,
      async (context?: BlockchainIdentifier | vscode.Uri | any) => {
        let commandArguments: CommandArguments = {};
        if (context && !!(context as vscode.Uri).fsPath) {
          // Activation was by right-click on an item in the VS Code file explorer
          commandArguments.path = (context as vscode.Uri).fsPath;
        } else if (context && !!(context as BlockchainIdentifier).name) {
          // Activation was by right-click on an item in the Blockchain explorer
          commandArguments.blockchainIdentifier =
            context as BlockchainIdentifier;
        } else if (context) {
          // Activation by command URI containing query string parameters
          commandArguments = await sanitizeCommandArguments(context);
        }
        await handler(commandArguments);
      }
    )
  );
}

export async function activate(context: vscode.ExtensionContext) {
  Log.log(LOG_PREFIX, "Activating extension...");
  const blockchainMonitorPool = new BlockchainMonitorPool();
  const walletDetector = new WalletDetector();
  const neoExpress = new NeoExpress(context);
  const serverListDetector = new ServerListDetector(context.extensionPath);
  const neoExpressDetector = new NeoExpressDetector(context.extensionPath);
  const blockchainsTreeDataProvider = await BlockchainsTreeDataProvider.create(
    neoExpressDetector,
    serverListDetector
  );
  const activeConnection = new ActiveConnection(
    blockchainsTreeDataProvider,
    blockchainMonitorPool
  );
  const contractDetector = new ContractDetector(activeConnection);
  const neoExpressInstanceManager = new NeoExpressInstanceManager(
    neoExpress,
    activeConnection
  );
  const autoComplete = new AutoComplete(
    context,
    neoExpress,
    activeConnection,
    contractDetector,
    walletDetector,
    neoExpressDetector
  );
  const walletsTreeDataProvider = new WalletsTreeDataProvider(
    context.extensionPath,
    activeConnection,
    walletDetector,
    autoComplete
  );
  const contractsTreeDataProvider = new ContractsTreeDataProvider(
    context.extensionPath,
    autoComplete,
    contractDetector
  );
  const neoInvokeFileEditorProvider = new NeoInvokeFileEditorProvider(
    context,
    activeConnection,
    neoExpress,
    autoComplete
  );
  const checkpointDetector = new CheckpointDetector();

  context.subscriptions.push(activeConnection);
  context.subscriptions.push(autoComplete);
  context.subscriptions.push(checkpointDetector);
  context.subscriptions.push(contractDetector);
  context.subscriptions.push(neoExpressDetector);
  context.subscriptions.push(neoExpressInstanceManager);
  context.subscriptions.push(serverListDetector);
  context.subscriptions.push(walletDetector);

  context.subscriptions.push(
    vscode.window.registerTreeDataProvider(
      "neo3-visual-devtracker.views.blockchains",
      blockchainsTreeDataProvider
    )
  );

  context.subscriptions.push(
    vscode.window.registerTreeDataProvider(
      "neo3-visual-devtracker.views.contracts",
      contractsTreeDataProvider
    )
  );

  context.subscriptions.push(
    vscode.window.registerTreeDataProvider(
      "neo3-visual-devtracker.views.wallets",
      walletsTreeDataProvider
    )
  );

  context.subscriptions.push(
    vscode.window.registerCustomEditorProvider(
      "neo3-visual-devtracker.neo.neo-invoke-json",
      neoInvokeFileEditorProvider
    )
  );

  context.subscriptions.push(
    vscode.window.registerWebviewViewProvider(
      "neo3-visual-devtracker.views.quickStart",
      new QuickStartViewProvider(
        context,
        blockchainsTreeDataProvider,
        neoExpressInstanceManager,
        contractDetector,
        activeConnection,
        walletDetector
      )
    )
  );

  registerCommand(context, "neo3-visual-devtracker.express.create", () =>
    NeoExpressCommands.create(
      context,
      neoExpress,
      neoExpressInstanceManager,
      autoComplete,
      blockchainMonitorPool,
      blockchainsTreeDataProvider
    )
  );

  registerCommand(context, "neo3-visual-devtracker.neo.newContract", () =>
    Templates.newContract(context)
  );

  registerCommand(context, "neo3-visual-devtracker.neo.walletCreate", () =>
    NeoCommands.createWallet()
  );

  registerCommand(context, "neo3-visual-devtracker.connect", () =>
    activeConnection.connect()
  );

  registerCommand(context, "neo3-visual-devtracker.customizeServerList", () =>
    serverListDetector.customize()
  );

  registerCommand(context, "neo3-visual-devtracker.disconnect", () =>
    activeConnection.disconnect()
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.express.contractDeploy",
    (commandArguments) =>
      NeoExpressCommands.contractDeploy(
        neoExpress,
        contractDetector,
        blockchainsTreeDataProvider,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.express.customCommand",
    (commandArguments) =>
      NeoExpressCommands.customCommand(
        neoExpress,
        blockchainsTreeDataProvider,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.express.createCheckpoint",
    (commandArguments) =>
      NeoExpressCommands.createCheckpoint(
        neoExpress,
        blockchainsTreeDataProvider,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.express.exploreStorage",
    (commandArguments) =>
      NeoExpressCommands.exploreStorage(
        context,
        autoComplete,
        blockchainMonitorPool,
        blockchainsTreeDataProvider,
        neoExpress,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.express.reset",
    (commandArguments) =>
      NeoExpressCommands.reset(
        neoExpress,
        neoExpressInstanceManager,
        blockchainsTreeDataProvider,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.express.restoreCheckpoint",
    (commandArguments) =>
      NeoExpressCommands.restoreCheckpoint(
        neoExpress,
        blockchainsTreeDataProvider,
        checkpointDetector,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.express.run",
    (commandArguments) =>
      neoExpressInstanceManager.run(
        blockchainsTreeDataProvider,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.express.runAdvanced",
    (commandArguments) =>
      neoExpressInstanceManager.runAdvanced(
        blockchainsTreeDataProvider,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.express.stop",
    (commandArguments) =>
      neoExpressInstanceManager.stop(
        blockchainsTreeDataProvider,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.express.transfer",
    (commandArguments) =>
      NeoExpressCommands.transfer(
        neoExpress,
        blockchainsTreeDataProvider,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.express.walletCreate",
    (commandArguments) =>
      NeoExpressCommands.walletCreate(
        neoExpress,
        blockchainsTreeDataProvider,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.neo.contractDeploy",
    (commandArguments) =>
      NeoCommands.contractDeploy(
        contractDetector,
        walletDetector,
        blockchainsTreeDataProvider,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.neo.invokeContract",
    (commandArguments) =>
      NeoCommands.invokeContract(
        activeConnection,
        blockchainsTreeDataProvider,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.tracker.openTracker",
    (commandArguments) =>
      TrackerCommands.openTracker(
        context,
        autoComplete,
        blockchainMonitorPool,
        blockchainsTreeDataProvider,
        commandArguments
      )
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.tracker.openContract",
    (commandArguments) =>
      TrackerCommands.openContract(context, autoComplete, commandArguments)
  );

  registerCommand(
    context,
    "neo3-visual-devtracker.tracker.openWallet",
    (commandArguments) =>
      TrackerCommands.openWallet(
        context,
        autoComplete,
        commandArguments,
        activeConnection
      )
  );
}

export function deactivate() {
  Log.log(LOG_PREFIX, "Deactivating extension...");
  Log.close();
}
