import React from "react";

import NavButton from "../NavButton";
import ScriptToken from "../tracker/ScriptToken";
import StorageExplorerViewRequest from "../../../shared/messages/storageExplorerViewRequest";
import StorageExplorerViewState from "../../../shared/viewState/storageExplorerViewState";

type Props = {
  viewState: StorageExplorerViewState;
  postMessage: (message: StorageExplorerViewRequest) => void;
};

export default function StorageExplorer({ viewState, postMessage }: Props) {
  const contractSelector = (
    <div
      style={{
        alignItems: "center",
        display: "flex",
        justifyContent: "space-between",
      }}
    >
      <div style={{ flex: "1 0", padding: 10 }}>
        <select
          onChange={(e) => postMessage({ selectContract: e.target.value })}
          style={{
            color: "var(--vscode-input-foreground)",
            backgroundColor: "var(--vscode-input-background)",
            border: "1px solid var(--vscode-input-border)",
            fontSize: "1.25rem",
            padding: 5,
            width: "100%",
          }}
          value={viewState.selectedContract || ""}
        >
          <option value=""></option>
          {viewState.contracts.map((_) => (
            <option key={_} value={_}>
              {_}
            </option>
          ))}
        </select>
      </div>
      <div style={{ flex: "0 0", padding: 10 }}>
        <NavButton onClick={() => postMessage({ refresh: true })}>
          Refresh
        </NavButton>
      </div>
    </div>
  );
  const cellStyle: React.CSSProperties = {
    paddingLeft: 10,
    textAlign: "left",
  };
  return (
    <div
      style={{
        alignItems: "stretch",
        display: "flex",
        flexDirection: "column",
        height: "100%",
      }}
    >
      <div>
        {contractSelector}
        {!!viewState.error && (
          <div style={{ color: "var(--vscode-errorForeground)" }}>
            {viewState.error}
          </div>
        )}
      </div>
      <div
        style={{
          flex: "none 1",
          fontFamily: "monospace",
          overflow: "auto",
          paddingLeft: 10,
          paddingRight: 10,
        }}
      >
        {!!viewState.storage.length && (
          <table>
            <thead>
              <tr>
                <th style={cellStyle}>Key</th>
                <th style={cellStyle}>Value</th>
                <th style={cellStyle}>&nbsp;</th>
              </tr>
            </thead>
            <tbody>
              {viewState.storage.map((_) => (
                <tr key={_.key}>
                  <td style={{ ...cellStyle, whiteSpace: "nowrap" }}>
                    {_.key ? _.key.substring(0, 2) : "(Unknown)"}{" "}
                    {_.key && _.key.length > 2 && (
                      <ScriptToken
                        autoCompleteData={viewState.autoCompleteData}
                        token={_.key.substring(2)}
                      />
                    )}
                  </td>
                  <td style={cellStyle}>
                    <ScriptToken
                      autoCompleteData={viewState.autoCompleteData}
                      token={_.value ? _.value : "(None)"}
                    />
                  </td>
                  <td style={cellStyle}>{_.constant ? "(Constant)" : ""}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
