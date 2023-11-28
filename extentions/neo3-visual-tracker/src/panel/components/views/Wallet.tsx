import React from "react";

import AddressDetails from "../tracker/AddressDetails";
import Hash from "../Hash";
import NavButton from "../NavButton";
import WalletViewState from "../../../shared/viewState/walletViewState";
import WalletViewRequest from "../../../shared/messages/walletViewRequest";

type Props = {
  viewState: WalletViewState;
  postMessage: (message: WalletViewRequest) => void;
};

export default function Wallet({ viewState, postMessage }: Props) {
  const address = viewState.address;
  const name =
    viewState.autoCompleteData.addressNames[address].join(", ") ||
    "Unknown wallet";
  return (
    <div style={{ padding: 10 }}>
      <h1>{name}</h1>
      <p style={{ paddingLeft: 20 }}>
        <div style={{ fontWeight: "bold", marginBottom: 10, marginTop: 15 }}>
          Address:
        </div>
        <div
          style={{ cursor: "pointer", paddingLeft: 20 }}
          onClick={() => postMessage({ copyAddress: true })}
        >
          <strong>
            <Hash hash={address} />
          </strong>{" "}
          <em> &mdash; click to copy address to clipboard</em>
        </div>
      </p>
      <p style={{ paddingLeft: 20 }}>
        <div style={{ fontWeight: "bold", marginBottom: 10, marginTop: 15 }}>
          Balance information:
        </div>
        {!!viewState.addressInfo && (
          <div
            style={{
              backgroundColor: "var(--vscode-editor-background)",
              color: "var(--vscode-editor-foreground)",
              border: "1px solid var(--vscode-focusBorder)",
              boxShadow: "-1px 1px 2px 0px var(--vscode-focusBorder)",
              borderRadius: 10,
              padding: 20,
              margin: 50,
              marginTop: 20,
              overflow: "auto",
            }}
          >
            <AddressDetails
              addressInfo={viewState.addressInfo}
              autoCompleteData={viewState.autoCompleteData}
            />
          </div>
        )}
        {!viewState.addressInfo && !viewState.offline && (
          <div style={{ paddingLeft: 20 }}>Loading balances&hellip;</div>
        )}
        {!viewState.addressInfo && viewState.offline && (
          <div style={{ paddingLeft: 20 }}>
            <em>(Connect to a blockchain to retrieve balances.)</em>
          </div>
        )}
      </p>
      <p style={{ paddingLeft: 20, textAlign: "center" }}>
        <NavButton onClick={() => postMessage({ refresh: true })}>
          Refresh
        </NavButton>
      </p>
    </div>
  );
}
