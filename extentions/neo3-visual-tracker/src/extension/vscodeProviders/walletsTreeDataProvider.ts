import * as vscode from "vscode";

import ActiveConnection from "../activeConnection";
import AutoComplete from "../autoComplete";
import Log from "../util/log";
import posixPath from "../util/posixPath";
import WalletDetector from "../fileDetectors/walletDetector";

const LOG_PREFIX = "WalletsTreeDataProvider";

type WalletData = {
  address: string;
  name: string;
  path: string;
  isNeoExpress: boolean;
};

export default class WalletsTreeDataProvider
  implements vscode.TreeDataProvider<WalletData>
{
  onDidChangeTreeData: vscode.Event<void>;

  private readonly onDidChangeTreeDataEmitter: vscode.EventEmitter<void>;

  private wallets: WalletData[] = [];

  constructor(
    private readonly extensionPath: string,
    private readonly activeConnection: ActiveConnection,
    private readonly walletDetector: WalletDetector,
    autoComplete: AutoComplete
  ) {
    this.onDidChangeTreeDataEmitter = new vscode.EventEmitter<void>();
    this.onDidChangeTreeData = this.onDidChangeTreeDataEmitter.event;
    activeConnection.onChange(() => this.refresh());
    walletDetector.onChange(() => this.refresh());
    autoComplete.onChange(() => this.refresh());
  }

  getTreeItem(wallet: WalletData): vscode.TreeItem {
    return {
      command: {
        command: "neo3-visual-devtracker.tracker.openWallet",
        arguments: [{ address: wallet.address }],
        title: wallet.address,
      },
      description: wallet.address,
      label: wallet.name,
      tooltip: `${wallet.address}\n${wallet.path}`,
      iconPath: wallet.isNeoExpress
        ? posixPath(this.extensionPath, "resources", "blockchain-express.svg")
        : posixPath(this.extensionPath, "resources", "blockchain-private.svg"),
    };
  }

  getChildren(wallet?: WalletData): WalletData[] {
    return wallet ? [] : this.wallets;
  }

  async refresh() {
    Log.log(LOG_PREFIX, "Refreshing wallet list");
    const newData: WalletData[] = [];
    for (const nep6Wallet of this.walletDetector.wallets) {
      for (const account of nep6Wallet.accounts) {
        newData.push({
          address: account.address,
          name: account.label,
          path: nep6Wallet.path,
          isNeoExpress: false,
        });
      }
    }
    const blockchainIdentifier =
      this.activeConnection.connection?.blockchainIdentifier;
    const expressWallets = await blockchainIdentifier?.getWalletAddresses();
    if (expressWallets) {
      for (const name of Object.keys(expressWallets)) {
        const address = expressWallets[name];
        newData.push({
          address,
          name,
          path: blockchainIdentifier?.configPath || "",
          isNeoExpress: true,
        });
      }
    }
    newData.sort((a, b) => a.name.localeCompare(b.name));
    this.wallets = newData;
    this.onDidChangeTreeDataEmitter.fire();
  }
}
