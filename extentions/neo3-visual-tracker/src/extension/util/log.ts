import * as vscode from "vscode";

const DEBUG = false;
const PREFIX_COLUMN_WIDTH = 20;

const startTimeMs = new Date().getTime();

let outputChannel: vscode.OutputChannel | null = null;

function secondsSinceStart() {
  return `${((new Date().getTime() - startTimeMs) / 1000).toFixed(2)}s`;
}

function truncate(logPrefix: string) {
  let result = logPrefix.substring(0, PREFIX_COLUMN_WIDTH);
  while (result.length < PREFIX_COLUMN_WIDTH + 1) {
    result += " ";
  }
  return result;
}

function log(
  level: "D" | "E" | "I" | "W",
  consoleLogger: (...args: any[]) => void,
  logPrefix: string,
  ...args: any[]
) {
  const prefix = `${secondsSinceStart()}\t${truncate(logPrefix)}`;
  consoleLogger(prefix, ...args);
  if (!outputChannel) {
    outputChannel = vscode.window.createOutputChannel(
      "Neo N3 Visual DevTracker"
    );
  }
  outputChannel.appendLine(
    `${level} ${prefix} ${args.map((_) => JSON.stringify(_)).join(" ")}`
  );
}

export default class Log {
  static close() {
    outputChannel?.dispose();
    outputChannel = null;
  }

  static debug(logPrefix: string, ...args: any[]) {
    if (DEBUG) {
      log(
        "D",
        console.debug,
        `${secondsSinceStart()}\t${truncate(logPrefix)}`,
        ...args
      );
    }
  }

  static error(logPrefix: string, ...args: any[]) {
    log(
      "E",
      console.error,
      `${secondsSinceStart()}\t${truncate(logPrefix)}`,
      ...args
    );
  }

  static log(logPrefix: string, ...args: any[]) {
    log(
      "I",
      console.log,
      `${secondsSinceStart()}\t${truncate(logPrefix)}`,
      ...args
    );
  }

  static warn(logPrefix: string, ...args: any[]) {
    log(
      "W",
      console.warn,
      `${secondsSinceStart()}\t${truncate(logPrefix)}`,
      ...args
    );
  }
}
