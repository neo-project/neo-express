import * as vscode from "vscode";

import ActiveConnection from "../activeConnection";
import AutoComplete from "../autoComplete";
import InvokeFilePanelController from "../panelControllers/invokeFilePanelController";
import NeoExpress from "../neoExpress/neoExpress";

export default class NeoInvokeFileEditorProvider
  implements vscode.CustomTextEditorProvider {
  // We store the timestamp of when we last opened an editor, and a reference to that editor.
  // This is part of a hacky way of knowing when we are rendering within a source-control "diff"
  // view.
  //
  // If you want to be the default editor for a file type, VS Code insists that you also
  // render both sides of the diff view, see: https://github.com/microsoft/vscode/issues/97683
  //
  // You can determine you are creating an editor for the "left" pane of a diff view
  // because the document's URI scheme will be "git". There is no way of knowing you are
  // creating an editor for the "right" pane, though; it is no different to creating a
  // regular editor for that file.
  //
  // It seems that the right pane is always created first, then the left pane shortly
  // after. Ee assume that any editor created in the 3 seconds preceding creation of a
  // git:// editor is also part of a diff view.
  private lastEditor: {
    controller: InvokeFilePanelController;
    timestamp: Date;
  } | null = null;

  constructor(
    private readonly context: vscode.ExtensionContext,
    private readonly activeConnection: ActiveConnection,
    private readonly neoExpress: NeoExpress,
    private readonly autoComplete: AutoComplete
  ) {}

  resolveCustomTextEditor(
    document: vscode.TextDocument,
    panel: vscode.WebviewPanel
  ): void | Thenable<void> {
    panel.webview.options = { enableScripts: true };
    const isPartOfDiffView = document.uri.scheme.toLowerCase() === "git";
    const isReadOnly = isPartOfDiffView;
    const timestamp = new Date();
    if (
      isPartOfDiffView &&
      this.lastEditor &&
      timestamp.getTime() - this.lastEditor.timestamp.getTime() < 3000
    ) {
      this.lastEditor.controller.isPartOfDiffView = true;
    }
    this.lastEditor = {
      controller: new InvokeFilePanelController(
        this.context,
        isPartOfDiffView,
        isReadOnly,
        this.neoExpress,
        document,
        this.activeConnection,
        this.autoComplete,
        panel
      ),
      timestamp,
    };
  }
}
