import React, { useEffect, useRef, useState } from "react";

import AutoCompleteData from "../../../shared/autoCompleteData";
import ContractTile from "./ContractTile";
import dedupeAndSort from "../../../extension/util/dedupeAndSort";
import InputNonDraggable from "../InputNonDraggable";

type Props = {
  autoCompleteData: AutoCompleteData;
  contract?: string;
  forceFocus?: boolean;
  isPartOfDiffView: boolean;
  isReadOnly: boolean;
  style?: React.CSSProperties;
  setContract: (newValue: string) => void;
};

export default function ContractInput({
  autoCompleteData,
  contract,
  forceFocus,
  isPartOfDiffView,
  isReadOnly,
  style,
  setContract,
}: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (forceFocus) {
      inputRef.current?.focus();
    }
  }, []);
  const [hasFocus, setHasFocus] = useState(false);
  const inputStyle: React.CSSProperties = {
    color: "var(--vscode-input-foreground)",
    backgroundColor: "var(--vscode-input-background)",
    border: "1px solid var(--vscode-input-border)",
    boxSizing: "border-box",
    width: "100%",
    fontSize: "1.0rem",
    fontWeight: "bold",
    padding: 5,
    marginTop: 5,
  };
  const akaStyle: React.CSSProperties = {
    marginTop: 5,
    marginLeft: 30,
    fontStyle: "italic",
  };
  const akaItemStyle: React.CSSProperties = {
    textDecoration: "underline",
    cursor: "pointer",
    marginTop: 3,
  };
  const dropdownStyle: React.CSSProperties = {
    position: "absolute",
    zIndex: 1,
    left: 20,
    right: 20,
    color: "var(--vscode-dropdown-foreground)",
    backgroundColor: "var(--vscode-dropdown-background)",
    borderBottom: "1px solid var(--vscode-dropdown-border)",
    borderLeft: "1px solid var(--vscode-dropdown-border)",
    borderRight: "1px solid var(--vscode-dropdown-border)",
    maxHeight: "80vh",
    overflow: "auto",
  };

  const allNamesAndHashes = dedupeAndSort(
    Object.keys(autoCompleteData.contractManifests).map((_) =>
      _.startsWith("0x") ? autoCompleteData.contractNames[_] || _ : _
    )
  );

  let contractHashOrName = contract || "";
  if (contractHashOrName.startsWith("#")) {
    contractHashOrName = contractHashOrName.substring(1);
  }

  let aka = autoCompleteData.contractNames[contractHashOrName] || "";

  return (
    <div style={{ ...style, position: "relative" }}>
      <InputNonDraggable
        disabled={isReadOnly}
        inputRef={inputRef}
        style={inputStyle}
        type="text"
        value={contract}
        onChange={(e) => setContract(e.target.value)}
        onFocus={() => setHasFocus(true)}
        onBlur={() => setHasFocus(false)}
      />
      {hasFocus && !!allNamesAndHashes.length && (
        <div style={dropdownStyle}>
          {allNamesAndHashes.map((contractHashOrName) => {
            return (
              <ContractTile
                key={contractHashOrName}
                contractHashOrName={contractHashOrName}
                autoCompleteData={autoCompleteData}
                onMouseDown={setContract}
              />
            );
          })}
        </div>
      )}
      {!isPartOfDiffView && !!aka && (
        <div style={akaStyle}>
          This contract can also be referred to as:{" "}
          <span style={akaItemStyle} onClick={() => setContract(aka)}>
            {aka}
          </span>
        </div>
      )}
    </div>
  );
}
