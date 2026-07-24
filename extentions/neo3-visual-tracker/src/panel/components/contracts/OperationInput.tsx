import React, { useRef, useState } from "react";

import * as neonSc from "@cityofzion/neon-core/lib/sc";

import InputNonDraggable from "../InputNonDraggable";
import OperationTile from "./OperationTile";

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
  const [hasFocus, setHasFocus] = useState(false);
  const inputId = useRef(
    `neo-operation-${Math.random().toString(36).slice(2)}`
  ).current;

  return (
    <div className="neo-field neo-combobox">
      <label className="neo-field__label" htmlFor={inputId}>
        Method
      </label>
      <InputNonDraggable
        className="neo-input"
        disabled={isReadOnly}
        id={inputId}
        type="text"
        value={operation || ""}
        onBlur={() => setHasFocus(false)}
        onChange={(event) => setOperation(event.target.value)}
        onFocus={() => setHasFocus(true)}
      />
      {hasFocus && !!operations.length && (
        <div className="neo-combobox__menu">
          {operations.map((candidate, index) => (
            <OperationTile
              key={`${candidate.name}-${index}`}
              operation={candidate}
              onMouseDown={setOperation}
            />
          ))}
        </div>
      )}
      {!operations.length && (
        <div className="neo-field__meta">
          Enter a method name or select a contract with a detected manifest.
        </div>
      )}
    </div>
  );
}
