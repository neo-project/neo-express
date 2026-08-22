import * as fs from "fs";
import * as path from "path";
import * as vscode from "vscode";

import ActiveConnection from "../activeConnection";
import AutoComplete from "../autoComplete";
import AutoCompleteData from "../../shared/autoCompleteData";
import ContractDetector from "../fileDetectors/contractDetector";
import InvokeFileViewRequest from "../../shared/messages/invokeFileViewRequest";
import InvokeFileViewState from "../../shared/viewState/invokeFileViewState";
import {
  areInvocationStepsReady,
  isLiveDebugWitnessScopeSupported,
  isWitnessScope,
  resolveSelectedAccount,
  toInvocationAccounts,
} from "../../shared/invocationExecution";
import IoHelpers from "../util/ioHelpers";
import indexContractState from "../../shared/indexContractState";
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
  private monitorChangeWatcher: vscode.Disposable | null = null;

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
        connectionHealthy: false,
        connectionName: null,
        errorText: "",
        executionAccounts: [],
        fileContents: [],
        fileContentsJson: "[]",
        isExpressConnection: false,
        isPartOfDiffView,
        isReadOnly,
        jsonMode: false,
        recentTransactions: [],
        selectedAccount: null,
        selectedTransactionId: null,
        witnessScope: "CalledByEntry",
      },
      context,
      panel
    );
    this.onFileUpdate();
    this.refreshExecutionContext();
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
      this.watchActiveMonitor();
      await this.refreshExecutionContext();
      await this.updateTransactionList();
    });
    this.watchActiveMonitor();
  }

  onClose() {
    if (this.changeWatcher) {
      this.changeWatcher.dispose();
      this.changeWatcher = null;
    }
    if (this.monitorChangeWatcher) {
      this.monitorChangeWatcher.dispose();
      this.monitorChangeWatcher = null;
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

    if (request.connect) {
      await this.activeConnection.connect(undefined, "express");
      await this.refreshExecutionContext();
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
      if (!areInvocationStepsReady(this.viewState.fileContents)) {
        vscode.window.showErrorMessage(
          "Configure a contract and method for every invocation before running all steps."
        );
      } else {
        await this.runFile(this.document.uri.fsPath, "All steps");
      }
    }

    if (request.runStep) {
      const fragment = this.viewState.fileContents[request.runStep.i];
      if (!areInvocationStepsReady([fragment])) {
        vscode.window.showErrorMessage(
          "Select a contract and method before running this invocation."
        );
      } else {
        await this.runFragment(fragment);
      }
    }

    if (request.selectTransaction) {
      await this.updateViewState({
        selectedTransactionId: request.selectTransaction.txid,
      });
    }

    if (request.selectAccount) {
      const selectedAccount = request.selectAccount.name;
      if (
        this.viewState.executionAccounts.some(
          (account) => account.name === selectedAccount
        )
      ) {
        await this.updateViewState({ selectedAccount });
      }
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

    if (
      request.updateWitnessScope &&
      isWitnessScope(request.updateWitnessScope.scope)
    ) {
      await this.updateViewState({
        witnessScope: request.updateWitnessScope.scope,
      });
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
    result.contractNames = { ...result.contractNames };

    const connection = this.activeConnection.connection;
    if (connection?.rpcClient) {
      for (const contractHash of this.viewState.fileContents
        .filter((_) => _.contract?.startsWith("0x"))
        .map((_) => _.contract || "")) {
        try {
          const contractState = await connection.rpcClient.getContractState(
            contractHash
          );
          indexContractState(result, contractState);
        } catch {}
      }
      for (const contractName of this.viewState.fileContents
        .filter((_) => !_.contract?.startsWith("0x"))
        .map((_) => _.contract || "")) {
        try {
          const contractStates = await ContractDetector.getContractStateByName(
            connection.rpcClient,
            contractName
          );
          if (contractStates.length === 1) {
            indexContractState(result, contractStates[0], contractName);
          } else if (contractStates.length > 1) {
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
      } catch (e : any) {
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

  private async runFile(filePath: string, operation?: string) {
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
      let account = this.viewState.selectedAccount || undefined;
      if (!account || !walletNames.includes(account)) {
        account = await IoHelpers.multipleChoice(
          "Select an account...",
          ...walletNames
        );
      }
      if (!account) {
        return;
      }
      const witnessScope = this.viewState.witnessScope;
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
            account,
            txid,
            blockchain: connection.blockchainIdentifier.name,
            operation,
            state: "pending",
            submittedAt: new Date().toISOString(),
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
      await this.runFile(tempFile, fragment?.operation || "Invocation");
    } catch (e : any) {
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
      } catch (e : any) {
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
    if (!isLiveDebugWitnessScopeSupported(this.viewState.witnessScope)) {
      vscode.window.showErrorMessage(
        "Live debugging currently supports CalledByEntry witness scope. Select CalledByEntry before debugging."
      );
      return;
    }

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
      let signerWalletName = this.viewState.selectedAccount || undefined;
      if (!signerWalletName || !wallets[signerWalletName]) {
        signerWalletName = await IoHelpers.multipleChoice(
          "Select an account",
          "(none)",
          ...Object.keys(wallets)
        );
      }
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
            const confirmed = !!(tx?.tx as any)?.blockhash;
            return {
              ..._,
              txid: _.txid,
              blockchain: _.blockchain,
              log: tx?.log,
              tx: tx?.tx,
              state: confirmed ? "confirmed" : ("pending" as TransactionStatus),
            };
          } catch (e : any) {
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

  private async refreshExecutionContext() {
    const connection = this.activeConnection.connection;
    const isExpressConnection =
      connection?.blockchainIdentifier.blockchainType === "express";
    const executionAccounts = isExpressConnection
      ? toInvocationAccounts(
          await connection.blockchainIdentifier.getWalletAddresses()
        )
      : [];
    await this.updateViewState({
      connectionHealthy: connection?.blockchainMonitor.healthy || false,
      connectionName: connection?.blockchainIdentifier.friendlyName || null,
      executionAccounts,
      isExpressConnection,
      selectedAccount: resolveSelectedAccount(
        executionAccounts,
        this.viewState.selectedAccount
      ),
    });
  }

  private watchActiveMonitor() {
    this.monitorChangeWatcher?.dispose();
    const monitor = this.activeConnection.connection?.blockchainMonitor;
    this.monitorChangeWatcher = monitor
      ? monitor.onChange(async () => {
          await this.updateViewState({ connectionHealthy: monitor.healthy });
          await this.updateTransactionList();
        })
      : null;
  }
}
