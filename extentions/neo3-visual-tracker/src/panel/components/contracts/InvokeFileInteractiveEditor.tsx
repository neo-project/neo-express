import React, { Fragment, useState } from "react";

import Dialog from "../Dialog";
import DropTarget from "../DropTarget";
import InvocationStep from "./InvocationStep";
import InvokeFileViewRequest from "../../../shared/messages/invokeFileViewRequest";
import InvokeFileViewState from "../../../shared/viewState/invokeFileViewState";
import NavButton from "../NavButton";
import TransactionList from "./TransactionList";

type Props = {
  viewState: InvokeFileViewState;
  postMessage: (message: InvokeFileViewRequest) => void;
};

export default function InvokeFileInteractiveEditor({
  viewState,
  postMessage,
}: Props) {
  const [dragActive, setDragActive] = useState(false);
  if (!!viewState.errorText) {
    return (
      <Dialog
        closeButtonText="Switch to JSON editor"
        title="There was a problem parsing this file"
        onClose={() => postMessage({ toggleJsonMode: true })}
      >
        <p>
          This file could not be parsed as a{" "}
          <span style={{ fontFamily: "monospace" }}>.neo-invoke.json</span>{" "}
          file. To continue editing the file, you will need to switch to the
          JSON editor.
        </p>
        <p>
          Once the JSON syntax error has been resolved, you can switch back and
          edit the file interactively.
        </p>
        <p
          style={{
            fontFamily: "monospace",
            fontSize: "0.8rem",
            color: "var(--vscode-errorForeground)",
          }}
        >
          {viewState.errorText}
        </p>
      </Dialog>
    );
  }
  const argumentSuggestionListId = `list_${Math.random()}`;
  const moveStep = (from: number, to: number) =>
    postMessage({ moveStep: { from, to } });
  return (
    <div
      style={{
        display: "flex",
        justifyContent: "space-between",
        alignItems: "stretch",
        height: "100%",
      }}
    >
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
            <option key={`hash_${contractHash}`} value={`${contractHash}`} />
          )
        )}
      </datalist>
      <div
        style={{
          flex: "2 0",
          overflow: "auto",
          backgroundColor: "var(--vscode-editor-background)",
          color: "var(--vscode-editor-foreground)",
          padding: 10,
        }}
      >
        {viewState.fileContents.map((_, i) => (
          <Fragment key={i}>
            <DropTarget i={i} onDrop={moveStep} dragActive={dragActive} />
            <InvocationStep
              isPartOfDiffView={viewState.isPartOfDiffView}
              isReadOnly={viewState.isReadOnly}
              i={i}
              forceFocus={i === 0 && !_.contract && !_.operation && !_.args}
              contract={_.contract}
              operation={_.operation}
              args={_.args}
              autoCompleteData={viewState.autoCompleteData}
              argumentSuggestionListId={argumentSuggestionListId}
              onDebug={() => postMessage({ debugStep: { i } })}
              onDelete={() => postMessage({ deleteStep: { i } })}
              onDragStart={() => setDragActive(true)}
              onDragEnd={() => setDragActive(false)}
              onRun={() => postMessage({ runStep: { i } })}
              onUpdate={(contract, operation, args) =>
                postMessage({
                  update: { i, contract, operation, args },
                })
              }
            />
          </Fragment>
        ))}
        <DropTarget
          i={viewState.fileContents.length}
          onDrop={moveStep}
          dragActive={dragActive}
        />
        {!!viewState.comments.length && (
          <div style={{ textAlign: "center", margin: 10 }}>
            <h4 style={{ margin: 0, marginBottom: 5 }}>Comments:</h4>
            {viewState.comments.map((_) => (
              <li key={_} style={{ marginBottom: 5 }}>
                {_.replace(/\/\//g, "")
                  .replace(/\/\*/g, "")
                  .replace(/\*\//g, "")}
              </li>
            ))}
          </div>
        )}
        {!viewState.isReadOnly && (
          <div style={{ textAlign: "center" }}>
            <NavButton onClick={() => postMessage({ addStep: true })}>
              Add step
            </NavButton>{" "}
            {!viewState.isPartOfDiffView && (
              <NavButton onClick={() => postMessage({ runAll: true })}>
                Run all steps
              </NavButton>
            )}
          </div>
        )}
      </div>
      {viewState.isPartOfDiffView && (
        <div
          style={{
            flex: "0 0",
            borderLeft: "1px solid var(--vscode-panel-border)",
          }}
        ></div>
      )}
      {!viewState.isPartOfDiffView && (
        <>
          <div
            style={{
              flex: "0 0",
              borderLeft: "1px solid var(--vscode-panel-border)",
              cursor: "pointer",
              backgroundColor: "var(--vscode-panel-background)",
            }}
            onClick={() => postMessage({ toggleTransactions: true })}
          >
            <div
              style={{
                width: 35,
                textAlign: "center",
                marginTop: 10,
                paddingTop: 10,
                paddingBottom: 14,
                borderRight: viewState.collapseTransactions
                  ? undefined
                  : "1px solid var(--vscode-panelTitle-activeBorder)",
              }}
            >
              {viewState.collapseTransactions ? "<" : ">"}
            </div>
          </div>
          {!viewState.collapseTransactions && (
            <div
              style={{
                flex: "1 1",
                overflow: "auto",
                padding: 10,
                paddingLeft: 15,
                paddingTop: 15,
                backgroundColor: "var(--vscode-panel-background)",
              }}
            >
              <TransactionList
                autoCompleteData={viewState.autoCompleteData}
                transactions={viewState.recentTransactions}
                selectedTransactionId={viewState.selectedTransactionId}
                onSelectTransaction={(txid) =>
                  postMessage({ selectTransaction: { txid } })
                }
              />
            </div>
          )}
        </>
      )}
    </div>
  );
}
