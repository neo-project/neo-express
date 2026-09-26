import * as childProcess from "child_process";
import * as fs from "fs";
import * as vscode from "vscode";
import * as which from "which";

import { formatCli } from "../util/redactCli";
import Log from "../util/log";
import NeoExpressTerminal from "./neoExpressTerminal";
import posixPath from "../util/posixPath";
import { watchForExpressStart } from "./watchExpressStart";

type Command =
  | "checkpoint"
  | "contract"
  | "create"
  | "reset"
  | "run"
  | "show"
  | "stop"
  | "transfer"
  | "transfernft"
  | "wallet"
  | "-v";

const DOTNET_CHECK_EXPIRY_IN_MS = 60000;
const LOG_PREFIX = "NeoExpress";
// create / reset / checkpoint / deploy routinely exceed a few seconds.
const TIMEOUT_IN_MS = 60000;
const TIMEOUT_POLLING_INTERVAL_IN_MS = 2000;
const MIN_DOTNET_MAJOR = 10;

export default class NeoExpress {
  private readonly binaryPath: string;
  private readonly dotnetPath: string;
  private readonly busyStatus: vscode.StatusBarItem;

  private runLock: boolean;
  private checkForDotNetPassedAt: number;
  private deployGasSupported: boolean | null;

  constructor(private readonly context: vscode.ExtensionContext) {
    this.binaryPath = NeoExpress.resolveBinaryPath(this.context.extensionPath);
    this.dotnetPath = which.sync("dotnet", { nothrow: true }) || "dotnet";
    this.runLock = false;
    this.checkForDotNetPassedAt = 0;
    this.deployGasSupported = null;
    this.busyStatus = vscode.window.createStatusBarItem(
      vscode.StatusBarAlignment.Left,
      40
    );
  }

  // A packed .NET tool places its assemblies under tools/<target-framework>/any/.
  // Resolve the framework folder at runtime so the path tracks the bundled
  // Neo.Express target framework instead of a hardcoded value that breaks
  // whenever the tool is repacked for a newer framework.
  private static resolveBinaryPath(extensionPath: string): string {
    const toolsRoot = posixPath(extensionPath, "deps", "nxp", "tools");
    try {
      const framework = fs
        .readdirSync(toolsRoot)
        .find((tfm) =>
          fs.existsSync(posixPath(toolsRoot, tfm, "any", "neoxp.dll"))
        );
      if (framework) {
        return posixPath(toolsRoot, framework, "any", "neoxp.dll");
      }
    } catch {
      // toolsRoot is missing or unreadable; fall through to a repo build or PATH.
    }
    const fromRepo = NeoExpress.findRepoNeoxp(extensionPath);
    if (fromRepo) {
      Log.log(
        LOG_PREFIX,
        `Bundled neoxp not found under ${toolsRoot}; using repo build: ${fromRepo}`
      );
      return fromRepo;
    }
    const fromPath = which.sync("neoxp", { nothrow: true });
    if (fromPath) {
      Log.log(
        LOG_PREFIX,
        `Bundled neoxp not found under ${toolsRoot}; using PATH: ${fromPath}`
      );
      return fromPath;
    }
    Log.error(
      LOG_PREFIX,
      `Could not locate bundled neoxp under ${toolsRoot} and neoxp is not on PATH.`
    );
    return posixPath(toolsRoot, "neoxp.dll");
  }

  private static findRepoNeoxp(extensionPath: string): string | null {
    const binRoot = posixPath(extensionPath, "..", "..", "src", "neoxp", "bin");
    for (const configuration of ["Debug", "Release"]) {
      const configurationDir = posixPath(binRoot, configuration);
      try {
        const tfm = fs
          .readdirSync(configurationDir)
          .find((name) =>
            fs.existsSync(posixPath(configurationDir, name, "neoxp.dll"))
          );
        if (tfm) {
          return posixPath(configurationDir, tfm, "neoxp.dll");
        }
      } catch {
        // This configuration has not been built.
      }
    }
    return null;
  }

  async supportsDeployGasOption(): Promise<boolean> {
    if (this.deployGasSupported !== null) {
      return this.deployGasSupported;
    }
    const help = await this.runUnlocked("contract", "deploy", "--help");
    this.deployGasSupported = /\s(-g|--gas)\b/i.test(help.message);
    return this.deployGasSupported;
  }

  private spawnArgs(commandArgs: string[]): { command: string; args: string[] } {
    if (this.binaryPath.toLowerCase().endsWith(".dll")) {
      return { command: this.dotnetPath, args: [this.binaryPath, ...commandArgs] };
    }
    return { command: this.binaryPath, args: commandArgs };
  }

  async runInTerminal(name: string, command: Command, ...options: string[]) {
    if (!(await this.checkForDotNetAsync())) {
      return null;
    }
    const spawned = this.spawnArgs([command, ...options]);
    const pty = new NeoExpressTerminal(spawned.command, spawned.args);
    const START_TIMEOUT_MS = 30000;
    const started = watchForExpressStart(pty, START_TIMEOUT_MS);
    const terminal = vscode.window.createTerminal({ name, pty });
    terminal.show();

    if (!(await started)) {
      Log.warn(
        LOG_PREFIX,
        `Neo Express did not start in ${name} (timeout or process exited)`
      );
      return null;
    }
    return terminal;
  }

  run(
    command: Command,
    ...options: string[]
  ): Promise<{ message: string; isError?: boolean }> {
    return this.runInDirectory(undefined, command, ...options);
  }

  runUnlocked(
    command: string,
    ...options: string[]
  ): Promise<{ message: string; isError?: boolean }> {
    return this.runUnsafe(undefined, command, ...options);
  }

  async runInDirectory(
    cwd: string | undefined,
    command: Command,
    ...options: string[]
  ): Promise<{ message: string; isError?: boolean }> {
    let durationInternal = 0;
    const startedAtExternal = new Date().getTime();
    const releaseLock = await this.getRunLock(`neoxp ${command}...`);
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
          `\`${formatCli("neoxp", [command, ...options])}\` took ${durationInternal}ms (${durationExternal}ms including time spent awaiting run-lock)`
        );
      }
    }
  }

  async runUnsafe(
    cwd: string | undefined,
    command: string,
    ...options: string[]
  ): Promise<{ message: string; isError?: boolean }> {
    if (!(await this.checkForDotNetAsync())) {
      return { message: "Could not launch Neo Express", isError: true };
    }
    const spawned = this.spawnArgs([...command.split(/\s/), ...options]);
    try {
      return new Promise((resolve) => {
        const startedAt = new Date().getTime();
        const process = childProcess.spawn(spawned.command, spawned.args, {
          cwd,
        });
        let complete = false;
        const watchdog = () => {
          if (!complete && new Date().getTime() - startedAt > TIMEOUT_IN_MS) {
            complete = true;
            try {
              process.kill();
            } catch (e : any) {
              Log.error(
                LOG_PREFIX,
                `Could not kill timed out neoxp command: ${command} (${e.message})`
              );
            }
            resolve({
              message: `Neo Express CLI timed out after ${TIMEOUT_IN_MS / 1000}s: ${formatCli(
                "neoxp",
                [command, ...options]
              )}`,
              isError: true,
            });
          } else if (!complete) {
            setTimeout(watchdog, TIMEOUT_POLLING_INTERVAL_IN_MS);
          }
        };
        watchdog();
        let message = "";
        const append = (chunk: Buffer | string) => {
          const text = chunk.toString();
          message = `${message}${text}`;
          Log.log(LOG_PREFIX, text.trimEnd());
        };
        process.stdout.on("data", append);
        process.stderr.on("data", append);
        process.on("close", (code) => {
          complete = true;
          resolve({ message, isError: code !== 0 });
        });
        process.on("error", (error) => {
          complete = true;
          resolve({
            message: error.message || "Failed to start Neo Express CLI",
            isError: true,
          });
        });
      });
    } catch (e : any) {
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

  private async checkForDotNetAsync() {
    const now = new Date().getTime();
    if (now - this.checkForDotNetPassedAt < DOTNET_CHECK_EXPIRY_IN_MS) {
      Log.debug(LOG_PREFIX, `checkForDotNetAsync skipped`);
      return true;
    }
    Log.log(LOG_PREFIX, `Checking for dotnet...`);
    let ok = false;
    try {
      ok =
        parseInt(
          childProcess.execFileSync(this.dotnetPath, ["--version"]).toString()
        ) >= MIN_DOTNET_MAJOR;
    } catch (e : any) {
      Log.error(LOG_PREFIX, "checkForDotNetAsync error:", e.message);
      ok = false;
    }
    if (ok) {
      this.checkForDotNetPassedAt = now;
    } else {
      const response = await vscode.window.showErrorMessage(
        ".NET 10 or higher is required to use this functionality.",
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

  private async getRunLock(label: string): Promise<() => void> {
    const started = Date.now();
    while (this.runLock) {
      const seconds = Math.round((Date.now() - started) / 1000);
      this.busyStatus.text = `$(sync~spin) Waiting for Neo Express CLI (${seconds}s)...`;
      this.busyStatus.show();
      await new Promise((resolve) => setTimeout(resolve, 200));
    }
    this.runLock = true;
    this.busyStatus.text = `$(sync~spin) ${label}`;
    this.busyStatus.show();
    return () => {
      this.runLock = false;
      this.busyStatus.hide();
    };
  }
}
