import React, { CSSProperties } from "react";

import ContractViewState from "../../../shared/viewState/contractViewState";
import ContractViewRequest from "../../../shared/messages/contractViewRequest";
import Hash from "../Hash";

type Props = {
  viewState: ContractViewState;
  postMessage: (message: ContractViewRequest) => void;
};

export default function Contract({ viewState, postMessage }: Props) {
  const hash = viewState.contractHash;
  const name =
    viewState.autoCompleteData.contractNames[hash] || "Unknown contract";
  const manifest = viewState.autoCompleteData.contractManifests[hash] || {};
  const extra = (manifest.extra || {}) as any;
  const description = extra["Description"] || undefined;
  const author = extra["Author"] || undefined;
  const email = extra["Email"] || undefined;
  const supportedStandards = manifest.supportedstandards || [];
  const contractPaths =
    viewState.autoCompleteData.contractPaths[hash] ||
    viewState.autoCompleteData.contractPaths[name] ||
    [];

  const [hovering, setHovering] = React.useState(false);

  const tooltipStyle: CSSProperties = {
    visibility: hovering ? "visible" : "hidden",
    minWidth: "300px", // adjust this value as needed or keep as 'auto'
    maxWidth: "600px", // prevent the tooltip from becoming too wide
    backgroundColor: "#555",
    color: "#fff",
    textAlign: "center",
    borderRadius: "6px",
    padding: "5px",

    /* Position the tooltip */
    position: "absolute",
    zIndex: 1,
    bottom: "150%" /* Place tooltip above the element */,
    left: "50%",
    transform: "translateX(-30%)", // Use transform to center the tooltip

    opacity: hovering ? 1 : 0,
    transition: "opacity 0.3s",
  };

  const iconStyle: CSSProperties = {
    position: "relative",
    display: "inline-flex",
    justifyContent: "center",
    alignItems: "center",
    width: "15px",
    height: "15px",
    cursor: "pointer",
    fontSize: "12px",
    fontWeight: "bold",
    borderRadius: "50%",
    backgroundColor: hovering ? "grey" : "white",
    color: "black",
    transition: "background-color 0.3s",
    marginLeft: "5px",
  };

  return (
    <div style={{ padding: 10 }}>
      <h1>{name}</h1>
      {!!description && (
        <p style={{ paddingLeft: 20 }}>
          <div style={{ fontWeight: "bold", marginBottom: 10, marginTop: 15 }}>
            Description:
          </div>
          <div style={{ paddingLeft: 20 }}>{description}</div>
        </p>
      )}
      {(!!author || !!email) && (
        <p style={{ paddingLeft: 20 }}>
          <div style={{ fontWeight: "bold", marginBottom: 10, marginTop: 15 }}>
            Author:
          </div>
          {!!author && <div style={{ paddingLeft: 20 }}>{author}</div>}
          {!!email && <div style={{ paddingLeft: 20 }}>&lt;{email}&gt;</div>}
        </p>
      )}
      <p style={{ paddingLeft: 20 }}>
        <div style={{ fontWeight: "bold", marginBottom: 10, marginTop: 15 }}>
          Hash:
        </div>
        <div
          style={{ cursor: "pointer", paddingLeft: 20 }}
          onClick={() => postMessage({ copyHash: true })}
        >
          <strong>
            <Hash hash={hash} />
          </strong>{" "}
          <em> &mdash; click to copy contract hash to clipboard</em>
        </div>
      </p>
      <p style={{ paddingLeft: 20 }}>
        <div style={{ fontWeight: "bold", marginBottom: 10, marginTop: 15 }}>
          <span>Hash (reversed):</span>
          <span
            style={iconStyle}
            onMouseEnter={() => setHovering(true)}
            onMouseLeave={() => setHovering(false)}
          >
            ?
            <span style={tooltipStyle}>
              Various tools in the Neo N3 ecosystem expect different byte order
              for contract hash strings. Please check the byte order expected by
              the tools you are using.
            </span>
          </span>
        </div>
        <div
          style={{ cursor: "pointer", paddingLeft: 20 }}
          onClick={() => postMessage({ copyHash: true, reverse: true })}
        >
          <strong>
            <Hash hash={hash} reverse={true} />
          </strong>{" "}
          <em> &mdash; click to copy contract hash to clipboard</em>
        </div>
      </p>
      {!!supportedStandards.length && (
        <p style={{ paddingLeft: 20 }}>
          <div style={{ fontWeight: "bold", marginBottom: 10, marginTop: 15 }}>
            Supported standards:
          </div>
          <ul>
            {supportedStandards.map((_, i) => (
              <li key={i}>{_}</li>
            ))}
          </ul>
        </p>
      )}
      {!!contractPaths.length && (
        <p style={{ paddingLeft: 20 }}>
          <div style={{ fontWeight: "bold", marginBottom: 10, marginTop: 15 }}>
            Byte code location:
          </div>
          <ul>
            {contractPaths.map((_, i) => (
              <li key={i}>{_}</li>
            ))}
          </ul>
        </p>
      )}
    </div>
  );
}
