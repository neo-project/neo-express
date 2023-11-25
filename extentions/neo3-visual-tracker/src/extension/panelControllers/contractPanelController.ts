import * as vscode from "vscode";

import AutoComplete from "../autoComplete";
import ContractViewRequest from "../../shared/messages/contractViewRequest";
import ContractViewState from "../../shared/viewState/contractViewState";
import Log from "../util/log";
import PanelControllerBase from "./panelControllerBase";
import { reverseHex } from "@cityofzion/neon-core/lib/u";

const LOG_PREFIX = "ContractPanelController";

export default class ContractPanelController extends PanelControllerBase<
  ContractViewState,
  ContractViewRequest
> {
  constructor(
    context: vscode.ExtensionContext,
    private readonly contractHash: string,
    autoComplete: AutoComplete
  ) {
    super(
      {
        view: "contract",
        panelTitle:
          autoComplete.data.contractNames[contractHash] || contractHash,
        autoCompleteData: autoComplete.data,
        contractHash,
      },
      context
    );
    autoComplete.onChange((autoCompleteData) => {
      const name = autoCompleteData.contractNames[contractHash] || contractHash;
      this.updateViewState({ panelTitle: name, autoCompleteData });
    });
  }

  onClose() {}

  protected async onRequest(request: ContractViewRequest) {
    Log.log(LOG_PREFIX, "Request:", request);
    if (request.copyHash) {
      let scriptHash = this.contractHash;
      if (request.reverse) {
        scriptHash = `0x${reverseHex(scriptHash.substring(2))}`;
      }
      await vscode.env.clipboard.writeText(scriptHash);
      vscode.window.showInformationMessage(
        `Contract hash copied to clipboard: ${scriptHash}`
      );
    }
  }
}
