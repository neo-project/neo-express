import * as vscode from "vscode";

import ActiveConnection from "../activeConnection";
import AutoComplete from "../autoComplete";
import Log from "../util/log";
import PanelControllerBase from "./panelControllerBase";
import WalletViewRequest from "../../shared/messages/walletViewRequest";
import WalletViewState from "../../shared/viewState/walletViewState";

const LOG_PREFIX = "WalletPanelController";

export default class WalletPanelController extends PanelControllerBase<
  WalletViewState,
  WalletViewRequest
> {
  constructor(
    context: vscode.ExtensionContext,
    private readonly address: string,
    autoComplete: AutoComplete,
    private readonly activeConnection: ActiveConnection
  ) {
    super(
      {
        view: "wallet",
        panelTitle: autoComplete.data.addressNames[address][0] || address,
        autoCompleteData: autoComplete.data,
        address,
        addressInfo: null,
        offline: false,
      },
      context
    );
    autoComplete.onChange((autoCompleteData) => {
      const name = autoComplete.data.addressNames[address][0] || address;
      this.updateViewState({ panelTitle: name, autoCompleteData });
    });
    activeConnection.onChange(() => this.updateBalances());
    this.updateBalances();
  }

  onClose() {}

  protected async onRequest(request: WalletViewRequest) {
    Log.log(LOG_PREFIX, "Request:", request);

    if (request.copyAddress) {
      await vscode.env.clipboard.writeText(this.address);
      vscode.window.showInformationMessage(
        `Wallet address copied to clipboard: ${this.address}`
      );
    }

    if (request.refresh) {
      await this.updateBalances();
    }
  }

  private async updateBalances() {
    const blockchainMonitor =
      this.activeConnection.connection?.blockchainMonitor;
    if (blockchainMonitor) {
      const addressInfo = await blockchainMonitor.getAddress(
        this.address,
        true
      );
      await this.updateViewState({ addressInfo, offline: false });
    } else {
      await this.updateViewState({ addressInfo: null, offline: true });
    }
  }
}
