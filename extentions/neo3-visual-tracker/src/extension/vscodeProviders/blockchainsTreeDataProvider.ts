import * as vscode from "vscode";

import BlockchainIdentifier from "../blockchainIdentifier";
import BlockchainType from "../blockchainType";
import IoHelpers from "../util/ioHelpers";
import Log from "../util/log";
import NeoExpressDetector from "../fileDetectors/neoExpressDetector";
import ServerListDetector from "../fileDetectors/serverListDetector";

const LOG_PREFIX = "BlockchainsTreeDataProvider";

export default class BlockchainsTreeDataProvider
  implements vscode.TreeDataProvider<BlockchainIdentifier>
{
  onDidChangeTreeData: vscode.Event<void>;

  private readonly onDidChangeTreeDataEmitter: vscode.EventEmitter<void>;

  private rootElements: BlockchainIdentifier[] = [];

  static async create(
    neoExpressDetector: NeoExpressDetector,
    serverListDetector: ServerListDetector
  ) {
    const blockchainsTreeDataProvider = new BlockchainsTreeDataProvider(
      neoExpressDetector,
      serverListDetector
    );
    await blockchainsTreeDataProvider.refresh();
    return blockchainsTreeDataProvider;
  }

  private constructor(
    private readonly neoExpressDetector: NeoExpressDetector,
    private readonly serverListDetector: ServerListDetector
  ) {
    this.onDidChangeTreeDataEmitter = new vscode.EventEmitter<void>();
    this.onDidChangeTreeData = this.onDidChangeTreeDataEmitter.event;
    neoExpressDetector.onChange(() => this.refresh());
    serverListDetector.onChange(() => this.refresh());
  }

  getTreeItem(element: BlockchainIdentifier): vscode.TreeItem {
    return element.getTreeItem();
  }

  getChildren(element?: BlockchainIdentifier): BlockchainIdentifier[] {
    if (element) {
      return element.getChildren();
    } else {
      return this.rootElements;
    }
  }

  async refresh() {
    Log.log(LOG_PREFIX, "Refreshing tree view...");
    this.rootElements = [
      ...this.serverListDetector.blockchains,
      ...this.neoExpressDetector.blockchains,
    ];
    this.onDidChangeTreeDataEmitter.fire();
  }

  async select(
    blockchainTypeFilter?: BlockchainType
  ): Promise<BlockchainIdentifier | undefined> {
    const candidates = this.rootElements.filter(
      (_) => !blockchainTypeFilter || _.blockchainType === blockchainTypeFilter
    );
    if (!candidates.length) {
      return;
    }
    if (candidates.length === 1) {
      return candidates[0];
    }
    const selection = await IoHelpers.multipleChoice(
      "Select a blockchain",
      ...candidates.map((_, i) => `${i} - ${_.name}`)
    );
    if (!selection) {
      return;
    }
    const selectedIndex = parseInt(selection);
    return candidates[selectedIndex];
  }
}
