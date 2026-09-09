import QuickStartViewState from "../../../shared/viewState/quickStartViewState";

export type QuickStartAction =
  | "createOrOpenWorkspace"
  | "openExplorer"
  | "createExpressInstance"
  | "startExpress"
  | "createExpressWallet"
  | "createContract"
  | "deployExpressContract"
  | "deployContract"
  | "resetExpress"
  | "invokeContract"
  | "connect"
  | "createWallet"
  | "transfer"
  | "transferNft"
  | "createCheckpoint"
  | "restoreCheckpoint";

export type QuickStartActionItem = {
  action: QuickStartAction;
  label: string;
  tooltip: string;
  command: string;
};

export function describeQuickStartAction(
  action: QuickStartAction,
  viewState: QuickStartViewState
): QuickStartActionItem {
  switch (action) {
    case "createExpressInstance":
      return {
        action,
        label: "Create a new Neo Express instance",
        tooltip:
          "Neo Express can be used to create private blockchains that you can use to test and debug your smart contracts locally.",
        command: "neo3-visual-devtracker.express.create",
      };
    case "startExpress":
      return {
        action,
        label: "Start Neo Express",
        tooltip:
          "You are not currently running an instance of Neo Express. Running Neo Express will allow you to deploy, test and debug your contracts locally.",
        command: "neo3-visual-devtracker.express.run",
      };
    case "createExpressWallet":
      return {
        action,
        label: "Create a Neo Express wallet",
        tooltip: "Create a wallet on the local Neo Express chain.",
        command: "neo3-visual-devtracker.express.walletCreate",
      };
    case "createContract":
      return {
        action,
        label: "New contract",
        tooltip:
          "Scaffold a C# (Blank, NEP-17, NEP-11, Oracle, Ownable), Python, or Java contract under contracts/.",
        command: "neo3-visual-devtracker.neo.newContract",
      };
    case "deployExpressContract":
      return {
        action,
        label: "Deploy a contract",
        tooltip: `Deploy a workspace .nef to ${
          viewState.connectionName || "this Neo Express instance"
        }.`,
        command: "neo3-visual-devtracker.express.contractDeploy",
      };
    case "deployContract":
      return {
        action,
        label: "Deploy a contract",
        tooltip: `Deploy a workspace .nef to ${
          viewState.connectionName || "this chain"
        }.`,
        command: "neo3-visual-devtracker.neo.contractDeploy",
      };
    case "resetExpress":
      return {
        action,
        label: "Reset Neo Express",
        tooltip:
          "Wipe the connected Neo Express chain and start a fresh genesis. Deployed contracts are removed.",
        command: "neo3-visual-devtracker.express.reset",
      };
    case "invokeContract":
      return {
        action,
        label: "Invoke a contract",
        tooltip: "Your contract is deployed. Invoke it from Contract Studio.",
        command: "neo3-visual-devtracker.neo.invokeContract",
      };
    case "connect":
      return {
        action,
        label: "Connect to a blockchain",
        tooltip:
          "When you connect to a blockchain you will be able to see autocomplete suggestions within VS Code based on contracts deployed to the blockchain.",
        command: "neo3-visual-devtracker.connect",
      };
    case "createWallet":
      return {
        action,
        label: "Create a NEP-6 wallet",
        tooltip:
          "In order to deploy your contracts to TestNet or MainNet you will need a NEP-6 wallet.",
        command: "neo3-visual-devtracker.neo.walletCreate",
      };
    case "transfer":
      return {
        action,
        label: "Transfer NEO, GAS, or NEP-17",
        tooltip:
          "Transfer NEO, GAS, or a NEP-17 token between Neo Express wallets (neoxp transfer).",
        command: "neo3-visual-devtracker.express.transfer",
      };
    case "transferNft":
      return {
        action,
        label: "Transfer NFT (NEP-11)",
        tooltip:
          "Transfer an NFT token id between wallets (neoxp transfernft).",
        command: "neo3-visual-devtracker.express.transferNft",
      };
    case "createCheckpoint":
      return {
        action,
        label: "Create a checkpoint",
        tooltip: "Save the current Neo Express chain state to a checkpoint file.",
        command: "neo3-visual-devtracker.express.createCheckpoint",
      };
    case "restoreCheckpoint":
      return {
        action,
        label: "Restore a checkpoint",
        tooltip: "Restore a previously saved Neo Express checkpoint.",
        command: "neo3-visual-devtracker.express.restoreCheckpoint",
      };
    case "createOrOpenWorkspace":
      return {
        action,
        label: "Open or create a workspace folder",
        tooltip:
          "Create a new folder for your project and then open that folder in Visual Studio Code.",
        command: "vscode.openFolder",
      };
    case "openExplorer":
      return {
        action,
        label: "Open a blockchain explorer",
        tooltip:
          "You have access to a Neo blockchain explorer within Visual Studio Code.",
        command: "neo3-visual-devtracker.tracker.openTracker",
      };
  }
}

export function getQuickStartActions(
  viewState: QuickStartViewState
): QuickStartAction[] {
  const actions: QuickStartAction[] = [];

  if (viewState.workspaceIsOpen) {
    if (viewState.hasNeoExpressInstance) {
      if (!viewState.neoExpressIsRunning) {
        actions.push("startExpress");
      }
      actions.push("createExpressWallet");
      actions.push("resetExpress");
    } else {
      actions.push("createExpressInstance");
    }

    actions.push("createContract");

    if (viewState.connectionName) {
      if (viewState.hasNeoExpressInstance && viewState.hasContracts) {
        actions.push("deployExpressContract");
      } else if (viewState.neoExpressDeploymentRequired) {
        actions.push("deployExpressContract");
      } else if (viewState.neoDeploymentRequired) {
        actions.push("deployContract");
      }
      if (viewState.hasDeployedContract) {
        actions.push("invokeContract");
      }
    } else {
      actions.push("connect");
    }

    if (!viewState.hasWallets) {
      actions.push("createWallet");
    }
    if (viewState.hasNeoExpressInstance) {
      actions.push("transfer");
      actions.push("transferNft");
    }

    if (viewState.hasCheckpointCompatibleNeoExpressInstance) {
      actions.push("createCheckpoint");
      if (viewState.hasCheckpoints) {
        actions.push("restoreCheckpoint");
      }
    }
  } else {
    actions.push("createOrOpenWorkspace");
  }

  actions.push("openExplorer");
  return actions;
}
