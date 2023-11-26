import * as vscode from "vscode";

import AutoComplete from "../autoComplete";
import ContractDetector from "../fileDetectors/contractDetector";
import Log from "../util/log";
import posixPath from "../util/posixPath";

const LOG_PREFIX = "ContractsTreeDataProvider";

type ContractData = {
  description: string;
  hash?: string;
  name: string;
};

export default class ContractsTreeDataProvider
  implements vscode.TreeDataProvider<ContractData>
{
  onDidChangeTreeData: vscode.Event<void>;

  private readonly onDidChangeTreeDataEmitter: vscode.EventEmitter<void>;

  private contracts: ContractData[] = [];

  constructor(
    private readonly extensionPath: string,
    private readonly autoComplete: AutoComplete,
    private readonly contractDetector: ContractDetector
  ) {
    this.onDidChangeTreeDataEmitter = new vscode.EventEmitter<void>();
    this.onDidChangeTreeData = this.onDidChangeTreeDataEmitter.event;
    autoComplete.onChange(() => this.refresh());
    contractDetector.onChange(() => this.refresh());
  }

  getTreeItem(contract: ContractData): vscode.TreeItem {
    return {
      command: contract.hash
        ? {
            command: "neo3-visual-devtracker.tracker.openContract",
            arguments: [{ hash: contract.hash }],
            title: contract.hash,
          }
        : undefined,
      label: contract.name,
      tooltip: `${contract.hash}\n${contract.description || ""}`.trim(),
      description: contract.description,
      iconPath: contract.hash
        ? posixPath(this.extensionPath, "resources", "blockchain-express.svg")
        : posixPath(this.extensionPath, "resources", "blockchain-private.svg"),
    };
  }

  getChildren(contractHash?: ContractData): ContractData[] {
    return contractHash ? [] : this.contracts;
  }

  refresh() {
    Log.log(LOG_PREFIX, "Refreshing contract list");
    const newData: ContractData[] = [];
    for (const hash of Object.keys(this.autoComplete.data.contractNames)) {
      const name = this.autoComplete.data.contractNames[hash] || hash;
      const manifest = this.autoComplete.data.contractManifests[hash] || {};
      const description =
        ((manifest.extra || {}) as any)["Description"] || undefined;
      newData.push({ hash, name, description });
    }
    const workspaceContracts = this.contractDetector.contracts;
    for (const name of Object.keys(workspaceContracts)) {
      const workspaceContract = workspaceContracts[name];
      const manifest = workspaceContract.manifest;
      const description =
        ((manifest.extra || {}) as any)["Description"] || undefined;
      if (!newData.find((_) => _.name === name)) {
        newData.push({ name, description });
      }
    }
    newData.sort((a, b) => a.name.localeCompare(b.name));
    this.contracts = newData;
    this.onDidChangeTreeDataEmitter.fire();
  }
}
