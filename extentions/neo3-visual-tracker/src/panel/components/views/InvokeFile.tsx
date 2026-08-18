import React from "react";

import InvokeFileInteractiveEditor from "../contracts/InvokeFileInteractiveEditor";
import InvokeFileJsonEditor from "../contracts/InvokeFileJsonEditor";
import InvokeFileViewRequest from "../../../shared/messages/invokeFileViewRequest";
import InvokeFileViewState from "../../../shared/viewState/invokeFileViewState";
import NavButton from "../NavButton";

type Props = {
  viewState: InvokeFileViewState;
  postMessage: (message: InvokeFileViewRequest) => void;
};

export default function InvokeFile({ viewState, postMessage }: Props) {
  const activeContract = viewState.fileContents[0]?.contract || "Invocation file";
  return (
    <div className="contract-studio">
      <header className="contract-studio__header">
        <div className="contract-studio__title-group">
          <i aria-hidden="true" className="codicon codicon-rocket" />
          <h1 className="contract-studio__title">Contract Studio</h1>
          <span className="contract-studio__subtitle">{activeContract}</span>
        </div>
        <div className="contract-studio__toolbar">
          {!viewState.isPartOfDiffView && (
            <NavButton
              ariaLabel={
                viewState.collapseTransactions
                  ? "Show recent transactions"
                  : "Hide recent transactions"
              }
              icon="history"
              iconOnly
              onClick={() => postMessage({ toggleTransactions: true })}
              title={
                viewState.collapseTransactions
                  ? "Show recent transactions"
                  : "Hide recent transactions"
              }
              variant="ghost"
            />
          )}
          <NavButton
            ariaLabel={
              viewState.jsonMode
                ? "Switch to interactive editor"
                : "Switch to JSON editor"
            }
            icon={viewState.jsonMode ? "layout" : "code"}
            iconOnly
            onClick={() => postMessage({ toggleJsonMode: true })}
            title={
              viewState.jsonMode
                ? "Switch to interactive editor"
                : "Switch to JSON editor"
            }
            variant="ghost"
          />
        </div>
      </header>
      <div className="contract-studio__content">
        {viewState.jsonMode && (
          <InvokeFileJsonEditor
            fileContentsJson={viewState.fileContentsJson}
            isReadOnly={viewState.isReadOnly}
            onUpdate={
              viewState.isReadOnly
                ? undefined
                : (updateJson) => postMessage({ updateJson })
            }
          />
        )}
        {!viewState.jsonMode && (
          <InvokeFileInteractiveEditor
            postMessage={postMessage}
            viewState={viewState}
          />
        )}
      </div>
      <footer className="studio-footer">
        <span className="studio-footer__mode">
          <i
            aria-hidden="true"
            className={`codicon codicon-${viewState.jsonMode ? "code" : "layout"}`}
          />
          {viewState.jsonMode ? "JSON editor" : "Interactive editor"}
        </span>
        <span className="studio-footer__note">
          Changes are saved to the invocation file
        </span>
      </footer>
    </div>
  );
}
