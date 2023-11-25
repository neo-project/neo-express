import * as fs from "fs";
import * as vscode from "vscode";

import ControllerRequest from "../../shared/messages/controllerRequest";
import Log from "../util/log";
import posixPath from "../util/posixPath";
import ViewRequest from "../../shared/messages/viewRequest";
import ViewStateBase from "../../shared/viewState/viewStateBase";

const LOG_PREFIX = "PanelControllerBase";

export default abstract class PanelControllerBase<
  TViewState extends ViewStateBase,
  TViewRequest
> {
  protected get isClosed() {
    return this.closed;
  }

  protected viewState: TViewState;

  private closed: boolean;

  private readonly postMessage: (request: ControllerRequest) => Promise<void>;

  private readonly setTitle: (newTitle: string) => void;

  constructor(
    initialViewState: TViewState,
    context: vscode.ExtensionContext,
    panel?: vscode.WebviewPanel | vscode.WebviewView
  ) {
    this.closed = false;
    this.viewState = { ...initialViewState };
    if (!panel) {
      panel = vscode.window.createWebviewPanel(
        `neo3-visual-tracker-${this.viewState.view}`,
        this.viewState.panelTitle,
        vscode.ViewColumn.Active,
        { enableScripts: true }
      );
      context.subscriptions.push(panel);
    }
    panel.onDidDispose(
      () => {
        this.closed = true;
        this.onClose();
      },
      null,
      context.subscriptions
    );
    (panel as any).iconPath = vscode.Uri.file(
      posixPath(context.extensionPath, "resources", "neo-logo.png")
    );
    panel.webview.onDidReceiveMessage(
      this.recieveRequest,
      this,
      context.subscriptions
    );
    this.postMessage = async (message) => {
      try {
        await panel?.webview.postMessage(message);
      } catch (e) {
        Log.error(
          LOG_PREFIX,
          `Could not post message: ${e.message || "Unknown error"}`
        );
      }
    };
    this.setTitle = (newTitle) => {
      if (panel) {
        panel.title = newTitle;
      }
    };
    panel.webview.html = fs
      .readFileSync(
        posixPath(context.extensionPath, "dist", "panel", "index.html")
      )
      .toString()
      .replace(
        "[BASE_HREF]",
        panel.webview
          .asWebviewUri(
            vscode.Uri.file(posixPath(context.extensionPath, "dist", "panel"))
          )
          .toString() + "/"
      );
  }

  abstract onClose(): void;

  protected abstract onRequest(request: TViewRequest): Promise<void>;

  protected async updateViewState(updates: Partial<TViewState>) {
    if (this.closed) {
      return;
    }
    const mergedViewState = { ...this.viewState, ...updates };
    if (JSON.stringify(mergedViewState) === JSON.stringify(this.viewState)) {
      return;
    }
    Log.log(
      LOG_PREFIX,
      "Update:",
      this.viewState.panelTitle,
      Object.keys(updates)
    );
    if (updates.panelTitle) {
      this.setTitle(updates.panelTitle);
    }
    this.viewState = mergedViewState;
    await this.sendRequest({ viewState: this.viewState });
  }

  private async recieveRequest(request: ViewRequest) {
    Log.log(
      LOG_PREFIX,
      "Received:",
      this.viewState.panelTitle,
      Object.keys(request)
    );
    if (request.retrieveViewState) {
      await this.sendRequest({ viewState: this.viewState });
    }
    if (request.typedRequest) {
      await this.sendRequest({ loadingState: { isLoading: true } });
      try {
        await this.onRequest(request.typedRequest);
      } finally {
        await this.sendRequest({ loadingState: { isLoading: false } });
      }
    }
  }

  private async sendRequest(request: ControllerRequest) {
    Log.log(
      LOG_PREFIX,
      "Sent:",
      this.viewState.panelTitle,
      Object.keys(request)
    );
    await this.postMessage(request);
  }
}
