import React from "react";

import InvokeFileInteractiveEditor from "../contracts/InvokeFileInteractiveEditor";
import InvokeFileJsonEditor from "../contracts/InvokeFileJsonEditor";
import InvokeFileViewRequest from "../../../shared/messages/invokeFileViewRequest";
import InvokeFileViewState from "../../../shared/viewState/invokeFileViewState";

type Props = {
  viewState: InvokeFileViewState;
  postMessage: (message: InvokeFileViewRequest) => void;
};

export default function InvokeFile({ viewState, postMessage }: Props) {
  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        justifyContent: "space-between",
        height: "calc(100% - 1px)",
        maxHeight: "calc(100% - 1px)",
        borderTop: "1px solid var(--vscode-panel-border)",
        backgroundColor: "var(--vscode-editor-background)",
        color: "var(--vscode-editor-foreground)",
      }}
    >
      <div style={{ flex: "2 0", overflow: "auto" }}>
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
      <div
        style={{
          flex: "0 1",
          backgroundColor: "var(--vscode-statusBar-background)",
          color: "var(--vscode-statusBar-foreground)",
          borderTop: "var(--vscode-statusBar-border)",
          padding: "3px 15px 3px 15px",
          fontSize: "0.8rem",
        }}
      >
        {viewState.jsonMode ? "JSON editor mode" : "Interactive mode"} ({" "}
        <span
          style={{ cursor: "pointer", textDecoration: "underline" }}
          onClick={() => postMessage({ toggleJsonMode: true })}
        >
          Switch
        </span>{" "}
        )
      </div>
    </div>
  );
}
