import * as vscode from "vscode";

import ActiveConnection from "../activeConnection";
import BlockchainsTreeDataProvider from "./blockchainsTreeDataProvider";
import ContractDetector from "../fileDetectors/contractDetector";
import CheckpointDetector from "../fileDetectors/checkpointDetector";
import NeoExpressInstanceManager from "../neoExpress/neoExpressInstanceManager";
import {
  describeQuickStartAction,
  getQuickStartActions,
  QuickStartActionItem,
} from "../../panel/components/views/quickStartActions";
import QuickStartViewState from "../../shared/viewState/quickStartViewState";
import WalletDetector from "../fileDetectors/walletDetector";

export default class QuickStartTreeDataProvider
  implements vscode.TreeDataProvider<QuickStartActionItem>
{
  onDidChangeTreeData: vscode.Event<void>;

  private readonly onDidChangeTreeDataEmitter: vscode.EventEmitter<void>;
  private items: QuickStartActionItem[] = [];

  constructor(
    private readonly blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    private readonly neoExpressInstanceManager: NeoExpressInstanceManager,
    private readonly checkpointDetector: CheckpointDetector,
    private readonly contractDetector: ContractDetector,
    private readonly activeConnection: ActiveConnection,
    private readonly walletDetector: WalletDetector
  ) {
    this.onDidChangeTreeDataEmitter = new vscode.EventEmitter<void>();
    this.onDidChangeTreeData = this.onDidChangeTreeDataEmitter.event;
    vscode.workspace.onDidChangeWorkspaceFolders(() => this.refresh());
    this.blockchainsTreeDataProvider.onDidChangeTreeData(() => this.refresh());
    this.neoExpressInstanceManager.onChange(() => this.refresh());
    this.checkpointDetector.onChange(() => this.refresh());
    this.contractDetector.onChange(() => this.refresh());
    this.activeConnection.onChange(() => this.refresh());
    this.walletDetector.onChange(() => this.refresh());
    this.refresh();
  }

  getTreeItem(item: QuickStartActionItem): vscode.TreeItem {
    if (!item.command) {
      return {
        label: " ",
        collapsibleState: vscode.TreeItemCollapsibleState.None,
      };
    }
    return {
      label: item.label,
      tooltip: item.tooltip,
      collapsibleState: vscode.TreeItemCollapsibleState.None,
      command: {
        command: item.command,
        title: item.label,
      },
    };
  }

  getChildren(item?: QuickStartActionItem): QuickStartActionItem[] {
    return item ? [] : this.items;
  }

  private async refresh() {
    const connectionName =
      this.activeConnection.connection?.blockchainIdentifier.friendlyName ||
      null;

    let neoDeploymentRequired = false;
    let neoExpressDeploymentRequired = false;
    const deploymentRequired =
      Object.values(this.contractDetector.contracts).filter(
        (_) => _.deploymentRequired
      ).length > 0;
    if (deploymentRequired) {
      if (
        this.activeConnection.connection?.blockchainIdentifier
          .blockchainType === "express"
      ) {
        neoExpressDeploymentRequired = true;
      } else {
        neoDeploymentRequired = true;
      }
    }

    const hasContracts =
      Object.keys(this.contractDetector.contracts).length > 0;

    const hasDeployedContract =
      Object.values(this.contractDetector.contracts).filter((_) => _.deployed)
        .length > 0;

    const neoExpressInstances = this.blockchainsTreeDataProvider
      .getChildren()
      .filter((_) => _.blockchainType === "express");

    const hasNeoExpressInstance = neoExpressInstances.length > 0;

    const hasCheckpointCompatibleNeoExpressInstance = (
      await Promise.all(neoExpressInstances.map((_) => _.isSingleNodeExpress()))
    ).some(Boolean);

    const viewState: QuickStartViewState = {
      view: "quickStart",
      panelTitle: "",
      connectionName,
      hasContracts,
      hasDeployedContract,
      hasNeoExpressInstance,
      hasWallets: this.walletDetector.wallets.length > 0,
      hasCheckpoints: this.checkpointDetector.checkpointFiles.length > 0,
      hasCheckpointCompatibleNeoExpressInstance,
      neoDeploymentRequired,
      neoExpressDeploymentRequired,
      neoExpressIsRunning:
        this.neoExpressInstanceManager.runningInstance?.blockchainType ===
          "express" ||
        (this.activeConnection.connection?.blockchainIdentifier
          .blockchainType === "express" &&
          !!this.activeConnection.connection?.blockchainMonitor.healthy),
      workspaceIsOpen: !!vscode.workspace.workspaceFolders?.length,
    };

    this.items = [
      ...getQuickStartActions(viewState).map((action) =>
        describeQuickStartAction(action, viewState)
      ),
      {
        action: "createOrOpenWorkspace",
        label: " ",
        tooltip: "",
        command: "",
      },
    ];
    this.onDidChangeTreeDataEmitter.fire();
  }
}
