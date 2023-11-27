import * as childProcess from "child_process";
import * as vscode from "vscode";
import * as which from "which";

import Log from "../util/log";
import NeoExpressTerminal from "./neoExpressTerminal";
import posixPath from "../util/posixPath";

type Command =
  | "checkpoint"
  | "contract"
  | "create"
  | "reset"
  | "run"
  | "show"
  | "transfer"
  | "wallet"
  | "-v";

const DOTNET_CHECK_EXPIRY_IN_MS = 60000;
const LOG_PREFIX = "NeoExpress";
const TIMEOUT_IN_MS = 5000;
const TIMEOUT_POLLING_INTERVAL_IN_MS = 2000;

export default class NeoExpress {
  private readonly binaryPath: string;
  private readonly dotnetPath: string;

  private runLock: boolean;
  private checkForDotNetPassedAt: number;

  constructor(private readonly context: vscode.ExtensionContext) {
    this.binaryPath = posixPath(
      this.context.extensionPath,
      "deps",
      "nxp",
      "tools",
      "net6.0",
      "any",
      "neoxp.dll"
    );
    this.dotnetPath = which.sync("dotnet", { nothrow: true }) || "dotnet";
    this.runLock = false;
    this.checkForDotNetPassedAt = 0;
  }

  async runInTerminal(name: string, command: Command, ...options: string[]) {
    if (!this.checkForDotNet()) {
      return null;
    }
    const dotNetArguments = [this.binaryPath, command, ...options];
    const pty = new NeoExpressTerminal(this.dotnetPath, dotNetArguments);
    const terminal = vscode.window.createTerminal({ name, pty });

    const hasStarted: Promise<void> = new Promise((resolve) => {
      pty.onDidWrite((data) => {
        if (data.indexOf("Neo express is running") !== -1) {
          resolve();
        }
      });
    });

    terminal.show();

    // Give the terminal a chance to get a lock on the blockchain before
    // starting to do any offline commands.
    await hasStarted;

    return terminal;
  }

  run(
    command: Command,
    ...options: string[]
  ): Promise<{ message: string; isError?: boolean }> {
    return this.runInDirectory(undefined, command, ...options);
  }

  async runInDirectory(
    cwd: string | undefined,
    command: Command,
    ...options: string[]
  ): Promise<{ message: string; isError?: boolean }> {
    let durationInternal = 0;
    const startedAtExternal = new Date().getTime();
    const releaseLock = await this.getRunLock();
    try {
      const startedAtInternal = new Date().getTime();
      const result = await this.runUnsafe(cwd, command, ...options);
      const endedAtInternal = new Date().getTime();
      durationInternal = endedAtInternal - startedAtInternal;
      return result;
    } finally {
      releaseLock();
      const endedAtExternal = new Date().getTime();
      const durationExternal = endedAtExternal - startedAtExternal;
      if (durationExternal > 1000) {
        Log.log(
          LOG_PREFIX,
          `\`neoexp ${command} ${options.join(
            " "
          )}\` took ${durationInternal}ms (${durationExternal}ms including time spent awaiting run-lock)`
        );
      }
    }
  }

  async runUnsafe(
    cwd: string | undefined,
    command: string,
    ...options: string[]
  ): Promise<{ message: string; isError?: boolean }> {
    if (!this.checkForDotNet()) {
      return { message: "Could not launch Neo Express", isError: true };
    }
    const dotNetArguments = [
      this.binaryPath,
      ...command.split(/\s/),
      ...options,
    ];
    try {
      return new Promise((resolve, reject) => {
        const startedAt = new Date().getTime();
        const process = childProcess.spawn(this.dotnetPath, dotNetArguments, {
          cwd,
        });
        let complete = false;
        const watchdog = () => {
          if (!complete && new Date().getTime() - startedAt > TIMEOUT_IN_MS) {
            complete = true;
            try {
              process.kill();
            } catch (e) {
              Log.error(
                LOG_PREFIX,
                `Could not kill timed out neoxp command: ${command} (${e.message})`
              );
            }
            reject("Operation timed out");
          } else if (!complete) {
            setTimeout(watchdog, TIMEOUT_POLLING_INTERVAL_IN_MS);
          }
        };
        watchdog();
        let message = "";
        process.stdout.on(
          "data",
          (d) => (message = `${message}${d.toString()}`)
        );
        process.stderr.on(
          "data",
          (d) => (message = `${message}${d.toString()}`)
        );
        process.on("close", (code) => {
          complete = true;
          resolve({ message, isError: code !== 0 });
        });
        process.on("error", () => {
          complete = true;
          reject();
        });
      });
    } catch (e) {
      return {
        isError: true,
        message:
          e.stderr?.toString() ||
          e.stdout?.toString() ||
          e.message ||
          "Unknown failure",
      };
    }
  }

  private async checkForDotNet() {
    const now = new Date().getTime();
    if (now - this.checkForDotNetPassedAt < DOTNET_CHECK_EXPIRY_IN_MS) {
      Log.debug(LOG_PREFIX, `checkForDotNet skipped`);
      return true;
    }
    Log.log(LOG_PREFIX, `Checking for dotnet...`);
    let ok = false;
    try {
      ok =
        parseInt(
          childProcess.execFileSync(this.dotnetPath, ["--version"]).toString()
        ) >= 5;
    } catch (e) {
      Log.error(LOG_PREFIX, "checkForDotNet error:", e.message);
      ok = false;
    }
    if (ok) {
      this.checkForDotNetPassedAt = now;
    } else {
      const response = await vscode.window.showErrorMessage(
        ".NET 5 or higher is required to use this functionality.",
        "Dismiss",
        "More info"
      );
      if (response === "More info") {
        await vscode.env.openExternal(
          vscode.Uri.parse("https://dotnet.microsoft.com/download")
        );
      }
    }
    Log.log(LOG_PREFIX, `Checking for dotnet ${ok ? "succeeded" : "failed"}`);
    return ok;
  }

  private async getRunLock(): Promise<() => void> {
    while (this.runLock) {
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
    this.runLock = true;
    return () => {
      this.runLock = false;
    };
  }
}
