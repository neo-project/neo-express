import React, { useState } from "react";

import * as neonSc from "@cityofzion/neon-core/lib/sc";

import InputNonDraggable from "../InputNonDraggable";
import OperationTile from "./OperationTile";

type Props = {
  isReadOnly: boolean;
  operation?: string;
  operations: neonSc.ContractMethodDefinitionJson[];
  style?: React.CSSProperties;
  setOperation: (newValue: string) => void;
};

export default function OperationInput({
  isReadOnly,
  operation,
  operations,
  style,
  setOperation,
}: Props) {
  const [hasFocus, setHasFocus] = useState(false);
  const inputStyle: React.CSSProperties = {
    color: "var(--vscode-input-foreground)",
    backgroundColor: "var(--vscode-input-background)",
    border: "1px solid var(--vscode-input-border)",
    boxSizing: "border-box",
    width: "calc(100% - 15px)",
    fontSize: "0.9rem",
    padding: 2,
    marginTop: 5,
    marginLeft: 15,
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
  return (
    <div style={{ ...style, position: "relative" }}>
      <div>
        <strong>Operation:</strong>
      </div>
      <InputNonDraggable
        disabled={isReadOnly}
        style={inputStyle}
        type="text"
        value={operation}
        onChange={(e) => setOperation(e.target.value)}
        onFocus={() => setHasFocus(true)}
        onBlur={() => setHasFocus(false)}
      />
      {hasFocus && !!operations.length && (
        <div style={dropdownStyle}>
          {operations.map((operation) => (
            <OperationTile
              key={operation.name}
              operation={operation}
              onMouseDown={setOperation}
            />
          ))}
        </div>
      )}
    </div>
  );
}
