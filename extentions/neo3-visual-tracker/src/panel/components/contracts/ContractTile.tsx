import React, { Fragment } from "react";

import AutoCompleteData from "../../../shared/autoCompleteData";

type Props = {
  contractHashOrName: string;
  autoCompleteData: AutoCompleteData;
  onMouseDown?: (newValue: string) => void;
};

export default function ContractTile({
  contractHashOrName,
  autoCompleteData,
  onMouseDown,
}: Props) {
  const style: React.CSSProperties = {
    borderBottom: "1px solid var(--vscode-dropdown-border)",
    backgroundColor: "var(--vscode-dropdown-background)",
    padding: 5,
    cursor: onMouseDown ? "pointer" : undefined,
  };
  const methodStyle: React.CSSProperties = {
    backgroundColor: "var(--vscode-button-background)",
    color: "var(--vscode-button-foreground)",
    paddingLeft: 10,
    paddingRight: 10,
    paddingTop: 2,
    paddingBottom: 2,
    marginRight: 10,
    borderRadius: 10,
  };
  const aka = autoCompleteData.contractNames[contractHashOrName];
  let manifest = autoCompleteData.contractManifests[contractHashOrName];
  if (!manifest) {
    for (const contractHash of Object.keys(autoCompleteData.contractNames)) {
      const contractName = autoCompleteData.contractNames[contractHash];
      if (contractName === contractHashOrName) {
        manifest = autoCompleteData.contractManifests[contractHash];
      }
    }
  }
  const methods = manifest?.abi?.methods?.map((_) => _.name) || [];
  return (
    <div
      style={style}
      onMouseDown={() => {
        if (onMouseDown) {
          onMouseDown(contractHashOrName);
        }
      }}
    >
      <div>
        <strong>{contractHashOrName}</strong> {!!aka && <span>({aka})</span>}
      </div>
      {!!methods.length && (
        <div
          style={{
            marginLeft: 10,
            marginBottom: 5,
            lineHeight: 2,
          }}
        >
          {methods.map((_) => (
            <Fragment key={_}>
              <span key={_} style={methodStyle}>
                {_}
              </span>{" "}
            </Fragment>
          ))}
        </div>
      )}
    </div>
  );
}
