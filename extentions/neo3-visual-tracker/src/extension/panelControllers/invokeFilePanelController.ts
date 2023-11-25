import * as fs from "fs";
import * as path from "path";
import * as vscode from "vscode";

import ActiveConnection from "../activeConnection";
import AutoComplete from "../autoComplete";
import AutoCompleteData from "../../shared/autoCompleteData";
import ContractDetector from "../fileDetectors/contractDetector";
import InvokeFileViewRequest from "../../shared/messages/invokeFileViewRequest";
import InvokeFileViewState from "../../shared/viewState/invokeFileViewState";
import IoHelpers from "../util/ioHelpers";
import JSONC from "../util/JSONC";
import Log from "../util/log";
import NeoExpress from "../neoExpress/neoExpress";
import PanelControllerBase from "./panelControllerBase";
import posixPath from "../util/posixPath";
import TransactionStatus from "../../shared/transactionStatus";

const LOG_PREFIX = "InvokeFilePanelController";
const MAX_RECENT_TXS = 10;

export default class InvokeFilePanelController extends PanelControllerBase<
  InvokeFileViewState,
  InvokeFileViewRequest
> {
  private changeWatcher: vscode.Disposable | null;

  constructor(
    context: vscode.ExtensionContext,
    public isPartOfDiffView: boolean,
    isReadOnly: boolean,
    private readonly neoExpress: NeoExpress,
    private readonly document: vscode.TextDocument,
    private readonly activeConnection: ActiveConnection,
    private readonly autoComplete: AutoComplete,
    private readonly panel: vscode.WebviewPanel
  ) {
    super(
      {
        view: "invokeFile",
        panelTitle: "Invoke File Editor",
        autoCompleteData: autoComplete.data,
        collapseTransactions: true,
        comments: [],
        errorText: "",
        fileContents: [],
        fileContentsJson: "[]",
        isPartOfDiffView,
        isReadOnly,
        jsonMode: false,
        recentTransactions: [],
        selectedTransactionId: null,
      },
      context,
      panel
    );
    this.onFileUpdate();
    this.changeWatcher = vscode.workspace.onDidChangeTextDocument((e) => {
      if (e.document.uri.toString() === document.uri.toString()) {
        this.onFileUpdate();
      }
    });
    this.autoComplete.onChange(async () => {
      await this.updateViewState({
        autoCompleteData: await this.augmentAutoCompleteData(
          this.autoComplete.data
        ),
      });
    });
    this.activeConnection.onChange(async () => {
      this.activeConnection.connection?.blockchainMonitor.onChange(() =>
        this.updateTransactionList()
      );
      await this.updateTransactionList();
    });
    this.activeConnection.connection?.blockchainMonitor.onChange(() =>
      this.updateTransactionList()
    );
  }

  onClose() {
    if (this.changeWatcher) {
      this.changeWatcher.dispose();
      this.changeWatcher = null;
    }
  }

  protected async onRequest(request: InvokeFileViewRequest) {
    if (request.addStep) {
      await this.applyEdit(
        JSONC.editJsonString(
          this.viewState.fileContentsJson,
          [this.viewState.fileContents.length],
          {
            contract: "",
            operation: "",
          }
        )
      );
    }

    if (request.close) {
      this.panel.dispose();
    }

    if (request.debugStep) {
      await this.runFragmentInDebugger(
        this.viewState.fileContents[request.debugStep.i]
      );
    }

    if (request.deleteStep) {
      await this.applyEdit(
        JSONC.editJsonString(
          this.viewState.fileContentsJson,
          [request.deleteStep.i],
          undefined
        )
      );
    }

    if (request.moveStep) {
      let { from, to } = request.moveStep;
      const fromStep = this.viewState.fileContents[from];
      const toStep = this.viewState.fileContents[to];
      await this.applyEdit(
        JSONC.editJsonString(
          JSONC.editJsonString(this.viewState.fileContentsJson, [to], fromStep),
          [from],
          toStep
        )
      );
    }

    if (request.runAll) {
      await this.runFile(this.document.uri.fsPath);
    }

    if (request.runStep) {
      await this.runFragment(this.viewState.fileContents[request.runStep.i]);
    }

    if (request.selectTransaction) {
      await this.updateViewState({
        selectedTransactionId: request.selectTransaction.txid,
      });
    }

    if (request.toggleJsonMode) {
      await this.updateViewState({
        jsonMode: !this.viewState.jsonMode,
      });
    }

    if (request.toggleTransactions) {
      await this.updateViewState({
        collapseTransactions: !this.viewState.collapseTransactions,
      });
    }

    if (request.update !== undefined) {
      let fileContentsJson = this.viewState.fileContentsJson;
      if (!Array.isArray(JSONC.parse(fileContentsJson))) {
        fileContentsJson = `[\n${fileContentsJson.trim()}\n]`;
      }
      let updatedJson = JSONC.editJsonString(
        JSONC.editJsonString(
          fileContentsJson,
          [request.update.i, "contract"],
          request.update.contract
        ),
        [request.update.i, "operation"],
        request.update.operation
      );
      if (!this.viewState.fileContents[request.update.i].args) {
        updatedJson = JSONC.editJsonString(
          updatedJson,
          [request.update.i, "args"],
          request.update.args
        );
      } else if (request.update.args) {
        for (let i = 0; i < request.update.args.length; i++) {
          updatedJson = JSONC.editJsonString(
            updatedJson,
            [request.update.i, "args", i],
            request.update.args[i]
          );
        }
        const oldArgs = this.viewState.fileContents[request.update.i].args;
        if (oldArgs?.length) {
          for (let i = request.update.args.length; i < oldArgs.length; i++) {
            updatedJson = JSONC.editJsonString(
              updatedJson,
              [request.update.i, "args", i],
              undefined
            );
          }
        }
      }
      await this.applyEdit(updatedJson);
    }

    if (request.updateJson !== undefined) {
      await this.applyEdit(request.updateJson);
    }
  }

  private async applyEdit(newFileContentsJson: string) {
    const edit = new vscode.WorkspaceEdit();
    edit.replace(
      this.document.uri,
      new vscode.Range(0, 0, this.document.lineCount, 0),
      newFileContentsJson
    );
    await vscode.workspace.applyEdit(edit);
  }

  private async augmentAutoCompleteData(
    data: AutoCompleteData
  ): Promise<AutoCompleteData> {
    const result = { ...data };
    result.contractManifests = { ...result.contractManifests };

    const connection = this.activeConnection.connection;
    if (connection?.rpcClient) {
      for (const contractHash of this.viewState.fileContents
        .filter((_) => _.contract?.startsWith("0x"))
        .map((_) => _.contract || "")) {
        try {
          const contractState = await connection.rpcClient.getContractState(
            contractHash
          );
          result.contractManifests[contractState.hash] = contractState.manifest;
        } catch {}
      }
      for (const contractName of this.viewState.fileContents
        .filter((_) => !_.contract?.startsWith("0x"))
        .map((_) => _.contract || "")) {
        try {
          const manifests = await ContractDetector.getContractStateByName(
            connection.rpcClient,
            contractName
          );
          if (manifests.length === 1) {
            result.contractManifests[manifests[0].abi.hash] = manifests[0];
          } else if (manifests.length > 1) {
            Log.warn(
              LOG_PREFIX,
              `Multiple contracts deployed to ${connection.blockchainIdentifier.friendlyName} with name ${contractName}`
            );
          }
        } catch {}
      }
    }

    return result;
  }

  private async onFileUpdate() {
    if (this.isClosed) {
      return;
    }
    try {
      let fileContentsJson = this.document.getText();
      if (fileContentsJson?.trim().length === 0) {
        fileContentsJson = "[]";
      }
      try {
        let fileContents = JSONC.parse(fileContentsJson) || [];
        if (!Array.isArray(fileContents)) {
          fileContents = [fileContents];
        }
        await this.updateViewState({
          fileContents,
          fileContentsJson,
          comments: JSONC.extractComments(fileContentsJson),
          errorText: "",
        });
      } catch (e) {
        await this.updateViewState({
          errorText: e.message || "Unknown error",
          fileContentsJson,
        });
        return;
      }
    } catch {
      await this.updateViewState({
        errorText: `There was an error reading ${posixPath(
          this.document.uri.fsPath
        )}`,
      });
      return;
    }
  }

  private async runFile(filePath: string) {
    let connection = this.activeConnection.connection;
    if (!connection) {
      await this.activeConnection.connect();
      connection = this.activeConnection.connection;
      if (
        connection &&
        connection.blockchainIdentifier.blockchainType === "express"
      ) {
        await this.updateTransactionList();
      }
    }
    if (
      connection &&
      connection.blockchainIdentifier.blockchainType === "express"
    ) {
      const walletNames = Object.keys(
        await connection.blockchainIdentifier.getWalletAddresses()
      );
      const account = await IoHelpers.multipleChoice(
        "Select an account...",
        ...walletNames
      );
      if (!account) {
        return;
      }
      let witnessScope = await IoHelpers.multipleChoice(
        "Select the witness scope for the transaction signature",
        "CalledByEntry",
        "Global",
        "None"
      );
      if (!witnessScope) {
        return;
      }
      await this.document.save();
      await this.updateViewState({ collapseTransactions: false });
      const result = await this.neoExpress.runInDirectory(
        path.dirname(this.document.uri.fsPath),
        "contract",
        "invoke",
        "-w",
        witnessScope,
        "-i",
        connection.blockchainIdentifier.configPath,
        filePath,
        account
      );
      if (result.isError) {
        // showErrorMessage is not await'ed so that the loading spinner does not hang:
        vscode.window.showErrorMessage(result.message);
      } else {
        const recentTransactions = [...this.viewState.recentTransactions];
        for (const txidMatch of ` ${result.message} `.matchAll(
          /\s0x[0-9a-f]+\s/gi
        )) {
          const txid = txidMatch[0].trim();
          recentTransactions.unshift({
            txid,
            blockchain: connection.blockchainIdentifier.name,
            state: "pending",
          });
        }
        if (recentTransactions.length > MAX_RECENT_TXS) {
          recentTransactions.length = MAX_RECENT_TXS;
        }
        await this.updateViewState({ recentTransactions });
      }
    } else {
      // showWarningMessage is not await'ed so that the loading spinner does not hang:
      vscode.window.showWarningMessage(
        "You must be connected to a Neo Express blockchain to invoke contracts. Support for TestNet and MainNet contract invocation is coming soon."
      );
    }
  }

  private async runFragment(fragment: any) {
    const invokeFilePath = this.document.uri.fsPath;
    const tempFile = posixPath(
      path.dirname(invokeFilePath),
      `.temp.${path.basename(invokeFilePath)}`
    );
    try {
      fs.writeFileSync(tempFile, JSONC.stringify([fragment]));
      await this.runFile(tempFile);
    } catch (e) {
      Log.warn(
        LOG_PREFIX,
        "Error running fragment",
        tempFile,
        fragment,
        e.message
      );
    } finally {
      try {
        fs.unlinkSync(tempFile);
      } catch (e) {
        Log.warn(
          LOG_PREFIX,
          "Could not delete temporary file",
          tempFile,
          e.message
        );
      }
    }
  }

  private async runFragmentInDebugger(fragment: any) {
    const contract: string = fragment?.contract || "";
    const operation: string = fragment?.operation || "";
    if (!contract || !operation) {
      vscode.window.showErrorMessage(
        "A contract and an operation must be selected to launch the debugger."
      );
      return;
    }

    const autoCompleteData = this.autoComplete.data;
    let contractHashOrName = contract;
    if (contractHashOrName.startsWith("#")) {
      contractHashOrName = contractHashOrName.substring(1);
    }
    const paths = autoCompleteData.contractPaths[contractHashOrName] || [];
    const program = paths[0] || "";
    if (!program) {
      vscode.window.showErrorMessage(
        "Could not resolve the .nef file for the selected contract in the current workspace."
      );
      return;
    }

    let signer: string | undefined = undefined;
    let connection = this.activeConnection.connection;
    if (
      !connection &&
      (await IoHelpers.yesNo(
        "Would you like to specify a signing account for the transaction?"
      ))
    ) {
      await this.activeConnection.connect(undefined, "express");
      connection = this.activeConnection.connection;
    }

    if (connection) {
      const wallets =
        await connection.blockchainIdentifier.getWalletAddresses();
      const signerWalletName = await IoHelpers.multipleChoice(
        "Select an account",
        "(none)",
        ...Object.keys(wallets)
      );
      if (signerWalletName && signerWalletName !== "(none)") {
        signer = wallets[signerWalletName] || undefined;
      }
    }

    const debugConfiguration: vscode.DebugConfiguration = {
      name: `${path.basename(this.document.uri.fsPath)}-${operation}`,
      type: "neo-contract",
      request: "launch",
      program,
      invocation: {
        operation,
        args: Array.isArray(fragment.args) ? fragment.args : [],
      },
      signers: signer ? [signer] : undefined,
      runtime: {
        witnesses: {
          "check-result": true,
        },
      },
    };
    if (!(await vscode.debug.startDebugging(undefined, debugConfiguration))) {
      vscode.window.showErrorMessage(
        "There was a problem launching the debugger."
      );
    }
  }

  private async updateTransactionList() {
    const connection = this.activeConnection.connection;

    const recentTransactions = await Promise.all(
      this.viewState.recentTransactions.map(async (_) => {
        if (_.tx && (_.tx as any).confirmations) {
          return _;
        } else {
          try {
            const tx = await connection?.blockchainMonitor.getTransaction(
              _.txid,
              true
            );
            const confirmed = !!(tx?.tx as any).blockhash;
            return {
              txid: _.txid,
              blockchain: _.blockchain,
              log: tx?.log,
              tx: tx?.tx,
              state: confirmed ? "confirmed" : ("pending" as TransactionStatus),
            };
          } catch (e) {
            return _;
          }
        }
      })
    );

    await this.updateViewState({
      autoCompleteData: await this.augmentAutoCompleteData(
        this.autoComplete.data
      ),
      recentTransactions,
      isPartOfDiffView: this.isPartOfDiffView,
    });
  }
}
