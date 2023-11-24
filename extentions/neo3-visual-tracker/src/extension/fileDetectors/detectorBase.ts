import * as vscode from "vscode";

import Log from "../util/log";
import posixPath from "../util/posixPath";

const LOG_PREFIX = "DetectorBase";

export default abstract class DetectorBase {
  private readonly fileSystemWatcher: vscode.FileSystemWatcher;

  protected readonly onChangeEmitter: vscode.EventEmitter<void>;

  onChange: vscode.Event<void>;

  private disposed = false;

  private allFiles: string[] = [];

  get isDisposed() {
    return this.disposed;
  }

  protected get files() {
    return [...this.allFiles];
  }

  constructor(private readonly searchPattern: string) {
    this.onChangeEmitter = new vscode.EventEmitter<void>();
    this.onChange = this.onChangeEmitter.event;
    this.refresh();
    this.fileSystemWatcher =
      vscode.workspace.createFileSystemWatcher(searchPattern);
    this.fileSystemWatcher.onDidChange(this.refresh, this);
    this.fileSystemWatcher.onDidCreate(this.refresh, this);
    this.fileSystemWatcher.onDidDelete(this.refresh, this);
  }

  dispose() {
    this.disposed = true;
    this.fileSystemWatcher.dispose();
    this.onChangeEmitter.dispose();
  }

  protected async processFiles(): Promise<boolean | void> {}

  async refresh() {
    Log.log(LOG_PREFIX, "Refreshing file list...", this.searchPattern);
    this.allFiles = (await vscode.workspace.findFiles(this.searchPattern)).map(
      (_) => posixPath(_.fsPath)
    );
    await this.processFiles();
    this.onChangeEmitter.fire();
  }
}
