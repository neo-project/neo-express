import * as neonCore from "@cityofzion/neon-core";
import * as vscode from "vscode";

import BlockchainIdentifier from "./blockchainIdentifier";
import BlockchainMonitor from "./blockchainMonitor/blockchainMonitor";
import BlockchainMonitorPool from "./blockchainMonitor/blockchainMonitorPool";
import BlockchainType from "./blockchainType";
import BlockchainsTreeDataProvider from "./vscodeProviders/blockchainsTreeDataProvider";
import IoHelpers from "./util/ioHelpers";
import Log from "./util/log";

const LOG_PREFIX = "ActiveConnection";
const STATUS_PREFIX = "Neo:";

export default class ActiveConnection {
  connection: {
    blockchainIdentifier: BlockchainIdentifier;
    blockchainMonitor: BlockchainMonitor;
    rpcClient: neonCore.rpc.RPCClient;
  } | null;

  onChange: vscode.Event<BlockchainIdentifier | null>;

  private readonly onChangeEmitter: vscode.EventEmitter<BlockchainIdentifier | null>;

  private statusBarItem: vscode.StatusBarItem;
  private visible = false;

  constructor(
    private readonly blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    private readonly blockchainMonitorPool: BlockchainMonitorPool
  ) {
    this.connection = null;
    this.onChangeEmitter =
      new vscode.EventEmitter<BlockchainIdentifier | null>();
    this.onChange = this.onChangeEmitter.event;
    this.statusBarItem = vscode.window.createStatusBarItem();
  }

  dispose() {
    this.onChangeEmitter.dispose();
    this.statusBarItem.dispose();
    this.disconnect();
  }

  async connect(
    blockchainIdentifier?: BlockchainIdentifier,
    blockchainTypeFilter?: BlockchainType
  ) {
    blockchainIdentifier =
      blockchainIdentifier ||
      (await this.blockchainsTreeDataProvider.select(blockchainTypeFilter));
    let rpcUrl = blockchainIdentifier?.rpcUrls[0];
    if (blockchainIdentifier && blockchainIdentifier.rpcUrls.length > 1) {
      rpcUrl = await IoHelpers.multipleChoice(
        "Select an RPC server to connect to",
        ...blockchainIdentifier.rpcUrls
      );
    }
    if (blockchainIdentifier && rpcUrl) {
      const blockchainMonitor = this.blockchainMonitorPool.getMonitor(rpcUrl);
      blockchainMonitor.onChange(() => this.updateConnectionState());
      const connection = {
        blockchainMonitor,
        blockchainIdentifier,
        rpcClient: new neonCore.rpc.RPCClient(rpcUrl),
      };
      this.connection = connection;
      await this.onChangeEmitter.fire(connection.blockchainIdentifier);
    } else {
      this.connection?.blockchainMonitor.dispose();
      this.connection = null;
      await this.onChangeEmitter.fire(null);
    }
    await this.updateConnectionState();
  }

  async disconnect(force?: boolean) {
    if (this.connection) {
      if (
        force ||
        (await IoHelpers.yesNo(
          `Disconnect from ${this.connection.blockchainIdentifier.friendlyName}?`
        ))
      ) {
        this.connection.blockchainMonitor.dispose();
        this.connection = null;
        await this.updateConnectionState();
        Log.log(LOG_PREFIX, "Firing change event (disconnection)");
        await this.onChangeEmitter.fire(null);
      }
    }
  }

  private async updateConnectionState() {
    const connection = this.connection;
    const blockchainMonitor = connection?.blockchainMonitor;
    if (connection && blockchainMonitor) {
      if (blockchainMonitor.healthy) {
        this.statusBarItem.text = `${STATUS_PREFIX} Connected to ${connection.blockchainIdentifier.friendlyName}`;
        this.statusBarItem.tooltip = "Click to disconnect";
        this.statusBarItem.color = new vscode.ThemeColor(
          "statusBarItem.prominentForeground"
        );
      } else {
        this.statusBarItem.text = `${STATUS_PREFIX} Connecting to ${connection.blockchainIdentifier.friendlyName}...`;
        this.statusBarItem.tooltip =
          "A connection cannot currently be established to the Neo blockchain RPC server";
        this.statusBarItem.color = new vscode.ThemeColor(
          "statusBarItem.remoteForeground"
        );
      }
      this.statusBarItem.command = "neo3-visual-devtracker.disconnect";
    } else {
      this.statusBarItem.text = `${STATUS_PREFIX} Not connected`;
      this.statusBarItem.tooltip = "Click to connect to a Neo blockchain";
      this.statusBarItem.color = new vscode.ThemeColor("statusBar.foreground");
      this.statusBarItem.command = "neo3-visual-devtracker.connect";
    }
    if (!this.visible) {
      this.statusBarItem.show();
      this.visible = true;
    }
  }
}
