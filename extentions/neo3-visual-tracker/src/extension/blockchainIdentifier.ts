import * as fs from "fs";
import * as path from "path";
import * as vscode from "vscode";

import BlockchainType from "./blockchainType";
import IoHelpers from "./util/ioHelpers";
import JSONC from "./util/JSONC";
import Log from "./util/log";
import posixPath from "./util/posixPath";
import { parseExpressWalletAddresses } from "../shared/expressWalletAddresses";

const LOG_PREFIX = "BlockchainIdentifier";

export default class BlockchainIdentifier {
  static fromNameAndUrls(
    extensionPath: string,
    name: string,
    rpcUrls: string[],
    isWellKnown: boolean
  ): BlockchainIdentifier {
    return new BlockchainIdentifier(
      extensionPath,
      isWellKnown ? "public" : "private",
      "parent",
      name,
      rpcUrls,
      0,
      ""
    );
  }

  static async fromNeoExpressConfig(
    extensionPath: string,
    configPath: string
  ): Promise<BlockchainIdentifier | undefined> {
    try {
      const neoExpressConfig = JSONC.parse(
        (await fs.promises.readFile(configPath)).toString()
      );
      const nodePorts = neoExpressConfig["consensus-nodes"]
        ?.map((_: any) => parseInt(_["rpc-port"]))
        .filter((_: any) => !!_);
      // nodePorts is undefined when the config has no "consensus-nodes" key;
      // guard the access so a partial config is skipped instead of throwing.
      if (!nodePorts?.length) {
        Log.log(LOG_PREFIX, "No RPC ports found", configPath);
        return undefined;
      }
      const relative = vscode.workspace.asRelativePath(configPath, false);
      const name =
        relative && relative !== configPath
          ? relative.replace(/\\/g, "/")
          : path.basename(configPath);
      return new BlockchainIdentifier(
        extensionPath,
        "express",
        "parent",
        name,
        nodePorts.map((_: number) => `http://127.0.0.1:${_}`),
        0,
        configPath
      );
    } catch (e : any) {
      Log.log(
        LOG_PREFIX,
        "Error parsing neo-express config",
        configPath,
        e.message
      );
      return undefined;
    }
  }

  get friendlyName() {
    return this.name.split(":")[0];
  }

  private constructor(
    private readonly extensionPath: string,
    public readonly blockchainType: BlockchainType,
    public readonly nodeType: "parent" | "child",
    public readonly name: string,
    public readonly rpcUrls: string[],
    public readonly index: number,
    public readonly configPath: string
  ) {}

  getChildren() {
    if (this.nodeType === "parent") {
      return this.rpcUrls.map(
        (_, i) =>
          new BlockchainIdentifier(
            this.extensionPath,
            this.blockchainType,
            "child",
            `${this.name}:${i}`,
            [_],
            i,
            this.configPath
          )
      );
    } else {
      return [];
    }
  }

  async isSingleNodeExpress(): Promise<boolean> {
    if (this.blockchainType !== "express" || !this.configPath) {
      return false;
    }
    try {
      const neoExpressConfig = JSONC.parse(
        (await fs.promises.readFile(this.configPath)).toString()
      );
      return neoExpressConfig["consensus-nodes"]?.length === 1;
    } catch (e : any) {
      Log.log(
        LOG_PREFIX,
        "Error parsing neo-express node count",
        this.configPath,
        e.message
      );
      return false;
    }
  }

  async getWalletAddresses(): Promise<{ [walletName: string]: string }> {
    if (this.blockchainType !== "express") {
      return {};
    }
    let result: { [walletName: string]: string } = {};
    try {
      const neoExpressConfig = JSONC.parse(
        (await fs.promises.readFile(this.configPath)).toString()
      );
      result = parseExpressWalletAddresses(neoExpressConfig);
    } catch (e : any) {
      Log.log(
        LOG_PREFIX,
        "Error parsing neo-express wallets",
        this.configPath,
        e.message
      );
    }
    return result;
  }

  getTreeItem() {
    if (this.nodeType === "parent") {
      const treeItem = new vscode.TreeItem(
        this.name,
        vscode.TreeItemCollapsibleState.Expanded
      );
      treeItem.contextValue = this.blockchainType;
      treeItem.iconPath = vscode.Uri.file(
        posixPath(
          this.extensionPath,
          "resources",
          `blockchain-${this.blockchainType}.svg`
        )
      );
      return treeItem;
    } else {
      const treeItem = new vscode.TreeItem(
        this.rpcUrls[0],
        vscode.TreeItemCollapsibleState.None
      );
      treeItem.contextValue = this.blockchainType;
      treeItem.iconPath = vscode.ThemeIcon.File;
      return treeItem;
    }
  }

  async selectRpcUrl(): Promise<string | undefined> {
    const children = this.getChildren();
    if (children.length === 1) {
      return await children[0].selectRpcUrl();
    } else if (children.length > 1) {
      const selection = await IoHelpers.multipleChoice(
        "Select an RPC server",
        ...children.map((_, i) => `${i} - ${_.name}`)
      );
      if (!selection) {
        return;
      }
      const selectedIndex = parseInt(selection);
      return await children[selectedIndex].selectRpcUrl();
    } else {
      return this.rpcUrls[0];
    }
  }
}
