import * as childProcess from "child_process";
import * as vscode from "vscode";

export default class NeoExpressTerminal {
  private readonly closeEmitter: vscode.EventEmitter<void | number>;
  private readonly writeEmitter: vscode.EventEmitter<string>;

  private dimensions: vscode.TerminalDimensions | undefined | null;
  private process: childProcess.ChildProcessWithoutNullStreams | null;

  constructor(
    private readonly shellPath: string,
    private readonly shellArgs: string[]
  ) {
    this.closeEmitter = new vscode.EventEmitter<void | number>();
    this.writeEmitter = new vscode.EventEmitter<string>();
    this.dimensions = null;
    this.process = null;
  }

  get onDidClose() {
    return this.closeEmitter.event;
  }

  get onDidWrite() {
    return this.writeEmitter.event;
  }

  close() {
    this.process?.kill();
  }

  handleInput(data: string) {
    if (data.charCodeAt(0) === 3) {
      // Ctrl+C
      this.close();
    } else if (this.process?.exitCode === null) {
      this.process.send(data);
    } else {
      this.closeEmitter.fire();
    }
  }

  open(initialDimensions: vscode.TerminalDimensions | undefined) {
    this.dimensions = initialDimensions;
    this.writeEmitter.fire(this.bold("\r\nStarting Neo Express...\r\n"));
    this.process = childProcess.spawn(this.shellPath, this.shellArgs);
    this.process.stdout.on("data", (d) =>
      this.writeEmitter.fire("\r\n" + d.toString().trim())
    );
    this.process.stderr.on("data", (d) =>
      this.writeEmitter.fire("\r\n" + d.toString().trim())
    );
    this.process.on("close", (code: number) => {
      this.writeEmitter.fire(
        this.bold(
          `\r\n\r\nNeo Express has exited with code ${code}.\r\nPress any key to close this terminal.`
        )
      );
      this.process = null;
    });
    this.process.on("error", () => {
      this.writeEmitter.fire(
        this.bold(
          `\r\n\r\nNeo Express encountered an error.\r\nPress any key to close this terminal.`
        )
      );
      this.process = null;
    });
  }

  setDimensions(dimensions: vscode.TerminalDimensions) {
    this.dimensions = dimensions;
  }

  private bold(text: string) {
    return `\x1b[1m${text}\x1b[0m`;
  }
}
