import React from "react";

import * as neonSc from "@cityofzion/neon-core/lib/sc";

type Props = {
  operation: neonSc.ContractMethodDefinitionJson;
  onMouseDown?: (newValue: string) => void;
};

export default function OperatonTile({ operation, onMouseDown }: Props) {
  const style: React.CSSProperties = {
    borderBottom: "1px solid var(--vscode-dropdown-border)",
    backgroundColor: "var(--vscode-dropdown-background)",
    padding: 5,
    cursor: onMouseDown ? "pointer" : undefined,
  };
  return (
    <div
      style={style}
      onMouseDown={() => {
        if (onMouseDown) {
          onMouseDown(operation.name);
        }
      }}
    >
      <div>
        <strong>{operation.name}</strong>: (
        {operation.parameters.map((_) => `${_.type} ${_.name}`).join(", ")}) →{" "}
        {operation.returntype}
      </div>
    </div>
  );
}
