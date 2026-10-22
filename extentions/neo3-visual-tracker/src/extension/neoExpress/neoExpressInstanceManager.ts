import * as vscode from "vscode";

import ActiveConnection from "../activeConnection";
import BlockchainIdentifier from "../blockchainIdentifier";
import BlockchainsTreeDataProvider from "../vscodeProviders/blockchainsTreeDataProvider";
import { CommandArguments } from "../commands/commandArguments";
import Log from "../util/log";
import NeoExpress from "./neoExpress";
import IoHelpers from "../util/ioHelpers";

const LOG_PREFIX = "NeoExpressInstanceManager";
// VS Code does not offer an event-driven mechanism for detecting when a user closes a terminal, so polling is required:
const REFRESH_INTERVAL_MS = 1000 * 2;

export default class NeoExpressInstanceManager {
  onChange: vscode.Event<void>;

  get runningInstance() {
    return this.running;
  }

  isRunning(identifier: BlockchainIdentifier) {
    return (
      this.running?.configPath === identifier.configPath ||
      this.activeConnection.connection?.blockchainIdentifier.configPath ===
        identifier.configPath
    );
  }

  private readonly onChangeEmitter: vscode.EventEmitter<void>;

  private disposed: boolean;
  private running: BlockchainIdentifier | null;
  private terminals: vscode.Terminal[];

  constructor(
    private readonly neoExpress: NeoExpress,
    private readonly activeConnection: ActiveConnection
  ) {
    this.disposed = false;
    this.running = null;
    this.terminals = [];
    this.onChangeEmitter = new vscode.EventEmitter<void>();
    this.onChange = this.onChangeEmitter.event;
    this.refreshLoop();
  }

  dispose() {
    this.disposed = true;
  }

  async run(
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    commandArguments: CommandArguments
  ) {
    const identifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express"));
    if (identifier?.blockchainType !== "express") {
      return;
    }

    const secondsPerBlock = commandArguments.secondsPerBlock || 3;

    await this.stopAll(identifier);

    const children = identifier.getChildren();
    if (children.length) {
      for (const child of children) {
        const terminal = await this.neoExpress.runInTerminal(
          child.name,
          "run",
          "-i",
          child.configPath,
          "-s",
          `${secondsPerBlock}`,
          "--node-index",
          `${child.index}`
        );
        if (terminal) {
          this.terminals.push(terminal);
        }
      }
    } else {
      const terminal = await this.neoExpress.runInTerminal(
        identifier.name,
        "run",
        "-i",
        identifier.configPath,
        "-s",
        `${secondsPerBlock}`,
        "--node-index",
        `${identifier.index}`
      );
      if (terminal) {
        this.terminals.push(terminal);
      }
    }

    if (!this.terminals.length) {
      this.running = null;
      vscode.window.showErrorMessage(
        "Neo Express failed to start. Check the terminal for details."
      );
      this.onChangeEmitter.fire();
      return;
    }

    this.running = identifier;
    await this.activeConnection.connect(identifier);
    this.onChangeEmitter.fire();
  }

  async runAdvanced(
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    commandArguments: CommandArguments
  ) {
    commandArguments.secondsPerBlock = await IoHelpers.enterNumber(
      "How often (in seconds) should new blocks be produced?",
      15,
      (n) =>
        Math.round(n) === n && n > 0 && n <= 3600
          ? null
          : "Enter a whole number between 1 and 3600"
    );
    await this.run(blockchainsTreeDataProvider, commandArguments);
  }

  async stop(
    blockchainsTreeDataProvider: BlockchainsTreeDataProvider,
    commandArguments: CommandArguments
  ) {
    const identifier =
      commandArguments?.blockchainIdentifier ||
      (await blockchainsTreeDataProvider.select("express"));
    if (identifier?.blockchainType !== "express") {
      return;
    }
    if (
      this.activeConnection.connection?.blockchainIdentifier.configPath ===
      identifier.configPath
    ) {
      await this.activeConnection.disconnect(true);
    }
    if (this.runningInstance?.configPath === identifier.configPath) {
      await this.stopAll();
    }
  }

  async stopAll(identifier?: BlockchainIdentifier) {
    const target = identifier || this.running;
    if (
      target &&
      this.activeConnection.connection?.blockchainIdentifier.configPath ===
        target.configPath
    ) {
      await this.activeConnection.disconnect(true);
    }

    if (target?.blockchainType === "express") {
      const output = await this.neoExpress.run(
        "stop",
        "--all",
        "-i",
        target.configPath
      );
      if (output.isError) {
        Log.warn(
          LOG_PREFIX,
          "Could not stop",
          target.name,
          output.message.trim()
        );
      }
    }

    try {
      const terminals = this.matchingTerminals(target);
      for (const terminal of terminals) {
        if (!terminal.exitStatus) {
          terminal.dispose();
        }
      }
      const deadline = Date.now() + 8000;
      while (
        terminals.some((terminal) => !terminal.exitStatus) &&
        Date.now() < deadline
      ) {
        await new Promise((resolve) => setTimeout(resolve, 200));
      }
    } catch (e : any) {
      Log.warn(
        LOG_PREFIX,
        "Could not stop",
        this.running?.name || "unknown",
        e.message
      );
    } finally {
      this.terminals = [];
      this.running = null;
      this.onChangeEmitter.fire();
    }
  }

  private matchingTerminals(target?: BlockchainIdentifier | null) {
    const known = new Set(this.terminals);
    if (target) {
      const names = new Set<string>([
        target.name,
        ...target.getChildren().map((child) => child.name),
      ]);
      for (const terminal of vscode.window.terminals) {
        if (names.has(terminal.name)) {
          known.add(terminal);
        }
      }
    }
    return [...known];
  }

  private async checkTerminals() {
    if (this.terminals.length > 0) {
      this.terminals = this.terminals.filter((_) => _.exitStatus === undefined);
      if (this.terminals.length === 0 && this.running) {
        this.running = null;
        this.onChangeEmitter.fire();
      }
    }
  }

  private async refreshLoop() {
    if (this.disposed) {
      return;
    }
    try {
      await this.checkTerminals();
    } finally {
      setTimeout(() => this.refreshLoop(), REFRESH_INTERVAL_MS);
    }
  }
}
