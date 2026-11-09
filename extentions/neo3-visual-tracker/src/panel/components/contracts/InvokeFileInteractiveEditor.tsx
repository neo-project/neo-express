import React, { Fragment, useMemo, useState } from "react";

import Dialog from "../Dialog";
import DropTarget from "../DropTarget";
import InvocationStep from "./InvocationStep";
import InvokeFileViewRequest from "../../../shared/messages/invokeFileViewRequest";
import InvokeFileViewState from "../../../shared/viewState/invokeFileViewState";
import NavButton from "../NavButton";
import TransactionList from "./TransactionList";
import {
  areInvocationStepsReady,
  isLiveDebugWitnessScopeSupported,
  witnessScopes,
} from "../../../shared/invocationExecution";

type Props = {
  viewState: InvokeFileViewState;
  postMessage: (message: InvokeFileViewRequest) => void;
};

export default function InvokeFileInteractiveEditor({
  viewState,
  postMessage,
}: Props) {
  const [dragActive, setDragActive] = useState(false);
  const argumentSuggestionListId = useMemo(
    () => `neo-argument-suggestions-${Math.random().toString(36).slice(2)}`,
    []
  );

  if (viewState.errorText) {
    return (
      <Dialog
        closeButtonText="Switch to JSON editor"
        title="Invocation file syntax error"
        onClose={() => postMessage({ toggleJsonMode: true })}
      >
        <p>
          Contract Studio could not parse this invocation file. Switch to the
          JSON editor, correct the syntax, and then return to the interactive
          editor.
        </p>
        <pre className="studio-error">{viewState.errorText}</pre>
      </Dialog>
    );
  }

  const moveStep = (from: number, to: number) =>
    postMessage({ moveStep: { from, to } });
  const executionReady =
    viewState.isExpressConnection &&
    viewState.connectionHealthy &&
    !!viewState.selectedAccount;
  const executionReadinessMessage = !viewState.isExpressConnection
    ? "Connect to a Neo Express blockchain to run this invocation."
    : !viewState.connectionHealthy
    ? "The selected Neo Express blockchain is not responding."
    : !viewState.selectedAccount
    ? "Select an account to sign this invocation."
    : "The invocation will be signed and relayed to the selected Neo Express blockchain.";
  const allStepsReady = areInvocationStepsReady(viewState.fileContents);
  const runAllReady = executionReady && allStepsReady;
  const runAllReadinessMessage = !allStepsReady
    ? "Configure a contract and method for every invocation before running all steps."
    : executionReadinessMessage;
  const debugScopeReady = isLiveDebugWitnessScopeSupported(
    viewState.witnessScope
  );

  return (
    <div className="contract-studio__interactive">
      <datalist id={argumentSuggestionListId}>
        {Object.keys(viewState.autoCompleteData.wellKnownAddresses).map(
          (addressName) => (
            <option key={`aname_${addressName}`} value={`@${addressName}`} />
          )
        )}
        {Object.keys(viewState.autoCompleteData.addressNames).map((address) => (
          <option key={`adr_${address}`} value={`@${address}`} />
        ))}
        {Object.values(viewState.autoCompleteData.contractNames).map(
          (contractName) => (
            <option key={`cname_${contractName}`} value={`#${contractName}`} />
          )
        )}
        {Object.keys(viewState.autoCompleteData.contractNames).map(
          (contractHash) => (
            <option key={`hash_${contractHash}`} value={contractHash} />
          )
        )}
      </datalist>

      <section className="execution-context" aria-label="Execution context">
        <div className="neo-field">
          <span className="neo-field__label">Network</span>
          {viewState.connectionName ? (
            <div className="execution-context__status">
              <span
                aria-hidden="true"
                className={`status-dot ${
                  viewState.connectionHealthy
                    ? "status-dot--healthy"
                    : "status-dot--warning"
                }`}
              />
              <span>{viewState.connectionName}</span>
              {!viewState.isExpressConnection && (
                <span>(not Neo Express)</span>
              )}
            </div>
          ) : (
            <NavButton
              icon="plug"
              onClick={() => postMessage({ connect: true })}
              variant="secondary"
            >
              Connect to Neo Express
            </NavButton>
          )}
        </div>

        <label className="neo-field">
          <span className="neo-field__label">Account</span>
          <select
            aria-label="Signing account"
            className="neo-select"
            disabled={!viewState.executionAccounts.length}
            onChange={(event) =>
              postMessage({ selectAccount: { name: event.target.value } })
            }
            value={viewState.selectedAccount || ""}
          >
            {!viewState.executionAccounts.length && (
              <option value="">No Neo Express accounts available</option>
            )}
            {viewState.executionAccounts.map((account) => (
              <option key={account.name} value={account.name}>
                {account.name} ({shortenValue(account.address)})
              </option>
            ))}
          </select>
        </label>

        <label className="neo-field">
          <span className="neo-field__label">Witness scope</span>
          <select
            aria-label="Witness scope"
            className="neo-select"
            onChange={(event) =>
              postMessage({
                updateWitnessScope: { scope: event.target.value },
              })
            }
            value={viewState.witnessScope}
          >
            {witnessScopes.map((scope) => (
              <option key={scope} value={scope}>
                {scope}
              </option>
            ))}
          </select>
        </label>

        {!!viewState.connectionName && !viewState.isExpressConnection && (
          <NavButton
            icon="debug-disconnect"
            onClick={() => postMessage({ connect: true })}
            variant="secondary"
          >
            Switch network
          </NavButton>
        )}
      </section>

      <div
        className={`contract-studio__workspace ${
          viewState.isPartOfDiffView || viewState.collapseTransactions
            ? "contract-studio__workspace--single"
            : ""
        }`}
      >
        <main className="contract-studio__steps">
          {viewState.fileContents.map((step, i) => (
            <Fragment key={i}>
              <DropTarget i={i} onDrop={moveStep} dragActive={dragActive} />
              <InvocationStep
                args={step.args}
                argumentSuggestionListId={argumentSuggestionListId}
                autoCompleteData={viewState.autoCompleteData}
                contract={step.contract}
                debugScopeReady={debugScopeReady}
                executionReadinessMessage={executionReadinessMessage}
                executionReady={executionReady}
                forceFocus={
                  i === 0 && !step.contract && !step.operation && !step.args
                }
                i={i}
                isPartOfDiffView={viewState.isPartOfDiffView}
                isReadOnly={viewState.isReadOnly}
                operation={step.operation}
                onDebug={() => postMessage({ debugStep: { i } })}
                onDelete={() => postMessage({ deleteStep: { i } })}
                onDragStart={() => setDragActive(true)}
                onDragEnd={() => setDragActive(false)}
                onRun={() => postMessage({ runStep: { i } })}
                onUpdate={(contract, operation, args) =>
                  postMessage({ update: { i, contract, operation, args } })
                }
              />
            </Fragment>
          ))}
          <DropTarget
            i={viewState.fileContents.length}
            onDrop={moveStep}
            dragActive={dragActive}
          />

          {!viewState.fileContents.length && (
            <div className="studio-empty-state">
              <strong>No invocation steps yet</strong>
              <span>Add a step to select a contract method and arguments.</span>
            </div>
          )}

          {!!viewState.comments.length && (
            <section className="studio-comments" aria-label="File comments">
              <strong>File comments</strong>
              <ul>
                {viewState.comments.map((comment, i) => (
                  <li key={`${i}-${comment}`}>
                    {comment
                      .replace(/\/\//g, "")
                      .replace(/\/\*/g, "")
                      .replace(/\*\//g, "")}
                  </li>
                ))}
              </ul>
            </section>
          )}

          {!viewState.isReadOnly && (
            <div className="invocation-step__actions">
              <NavButton
                icon="add"
                onClick={() => postMessage({ addStep: true })}
                variant="secondary"
              >
                Add step
              </NavButton>
              {!viewState.isPartOfDiffView && viewState.fileContents.length > 1 && (
                <NavButton
                  disabled={!runAllReady}
                  icon="run-all"
                  onClick={() => postMessage({ runAll: true })}
                  title={runAllReadinessMessage}
                >
                  Run all steps
                </NavButton>
              )}
            </div>
          )}
        </main>

        {!viewState.isPartOfDiffView && !viewState.collapseTransactions && (
          <aside
            aria-label="Recent transactions"
            className="contract-studio__transactions"
          >
            <TransactionList
              autoCompleteData={viewState.autoCompleteData}
              transactions={viewState.recentTransactions}
              selectedTransactionId={viewState.selectedTransactionId}
              onSelectTransaction={(txid) =>
                postMessage({ selectTransaction: { txid } })
              }
            />
          </aside>
        )}
      </div>
    </div>
  );
}

function shortenValue(value: string) {
  if (value.length <= 18) {
    return value;
  }
  return `${value.slice(0, 8)}...${value.slice(-6)}`;
}
