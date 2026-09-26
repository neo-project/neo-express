import React, { useRef } from "react";

import * as neonSc from "@cityofzion/neon-core/lib/sc";

type Props = {
  isReadOnly: boolean;
  operation?: string;
  operations: neonSc.ContractMethodDefinitionJson[];
  setOperation: (newValue: string) => void;
};

export default function OperationInput({
  isReadOnly,
  operation,
  operations,
  setOperation,
}: Props) {
  const inputId = useRef(
    `neo-operation-${Math.random().toString(36).slice(2)}`
  ).current;

  if (operations.length) {
    const known = operations.some((candidate) => candidate.name === operation);
    return (
      <div className="neo-field">
        <label className="neo-field__label" htmlFor={inputId}>
          Method
        </label>
        <select
          className="neo-select"
          disabled={isReadOnly}
          id={inputId}
          value={known ? operation : ""}
          onChange={(event) => setOperation(event.target.value)}
        >
          <option value="">Select a method…</option>
          {operations.map((candidate, index) => (
            <option
              key={`${candidate.name}-${index}`}
              value={candidate.name}
            >
              {candidate.name}
              {candidate.parameters?.length
                ? ` (${candidate.parameters.map((p) => p.name).join(", ")})`
                : ""}
            </option>
          ))}
        </select>
      </div>
    );
  }

  return (
    <div className="neo-field">
      <label className="neo-field__label" htmlFor={inputId}>
        Method
      </label>
      <input
        className="neo-input"
        disabled={isReadOnly}
        id={inputId}
        type="text"
        value={operation || ""}
        onChange={(event) => setOperation(event.target.value)}
      />
      <div className="neo-field__meta">
        No ABI methods found for this contract. Build the project so a
        .manifest.json sits next to the .nef, or pick a deployed contract.
      </div>
    </div>
  );
}
