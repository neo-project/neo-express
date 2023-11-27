import * as vscode from "vscode";

import posixPath from "./posixPath";

export default function workspaceFolder() {
  const workspaceFolders = vscode.workspace.workspaceFolders;
  if (!workspaceFolders || !workspaceFolders.length) {
    return null;
  }
  return posixPath(workspaceFolders[0].uri.fsPath);
}
