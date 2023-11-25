import * as vscode from "vscode";

import AutoComplete from "../autoComplete";
import BlockchainIdentifier from "../blockchainIdentifier";
import BlockchainMonitor from "../blockchainMonitor/blockchainMonitor";
import BlockchainMonitorPool from "../blockchainMonitor/blockchainMonitorPool";
import Log from "../util/log";
import NeoExpress from "../neoExpress/neoExpress";
import NeoExpressIo from "../neoExpress/neoExpressIo";
import PanelControllerBase from "./panelControllerBase";
import StorageExplorerViewRequest from "../../shared/messages/storageExplorerViewRequest";
import StorageExplorerViewState from "../../shared/viewState/storageExplorerViewState";

const LOG_PREFIX = "StorageExplorerPanelController";

export default class StorageExplorerPanelController extends PanelControllerBase<
  StorageExplorerViewState,
  StorageExplorerViewRequest
> {
  private blockchainMonitor: BlockchainMonitor | null = null;

  constructor(
    context: vscode.ExtensionContext,
    private readonly identifier: BlockchainIdentifier,
    private readonly autoComplete: AutoComplete,
    blockchainMonitorPool: BlockchainMonitorPool,
    rpcUrl: string | undefined,
    private readonly neoExpress: NeoExpress
  ) {
    super(
      {
        autoCompleteData: autoComplete.data,
        contracts: [],
        error: null,
        panelTitle: `Storage Explorer: ${identifier.friendlyName}`,
        selectedContract: null,
        storage: [],
        view: "storageExplorer",
      },
      context
    );
    if (rpcUrl) {
      this.blockchainMonitor = blockchainMonitorPool.getMonitor(rpcUrl);
      this.blockchainMonitor.onChange(async () => await this.refresh());
    }
    autoComplete.onChange(() => this.refresh());
    this.refresh();
  }

  onClose() {
    if (this.blockchainMonitor) {
      this.blockchainMonitor.dispose();
    }
  }

  protected async onRequest(request: StorageExplorerViewRequest) {
    if (!!request.selectContract) {
      await this.updateViewState({ selectedContract: request.selectContract });
      await this.refresh();
    }

    if (!!request.refresh) {
      await this.refresh();
    }
  }

  private async refresh() {
    let updates: Partial<StorageExplorerViewState> = {
      autoCompleteData: this.autoComplete.data,
      error: null,
    };

    try {
      updates.contracts = Object.keys(
        await NeoExpressIo.contractList(this.neoExpress, this.identifier)
      );
      if (
        !!this.viewState.selectedContract &&
        updates.contracts.indexOf(this.viewState.selectedContract) === -1
      ) {
        updates.selectedContract = null;
      }
    } catch (e) {
      await this.updateViewState({
        contracts: [],
        error: e.message || "Unknown error",
        selectedContract: null,
        storage: [],
      });
      return;
    }

    const selectedContract =
      updates.selectedContract || this.viewState.selectedContract;
    try {
      if (!!selectedContract) {
        Log.debug(
          LOG_PREFIX,
          `Refreshing storage data for ${selectedContract}`
        );
        try {
          updates.storage = await NeoExpressIo.contractStorage(
            this.neoExpress,
            this.identifier,
            selectedContract
          );
        } catch (e) {
          updates.error = e.message || "Unknown error";
          updates.storage = [];
        }
      } else {
        updates.storage = [];
      }
    } finally {
      await this.updateViewState(updates);
    }
  }
}
