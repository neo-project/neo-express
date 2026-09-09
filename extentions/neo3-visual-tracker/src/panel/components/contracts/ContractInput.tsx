import React, { useEffect, useRef } from "react";

import AutoCompleteData from "../../../shared/autoCompleteData";
import dedupeAndSort from "../../../extension/util/dedupeAndSort";

type Props = {
  autoCompleteData: AutoCompleteData;
  contract?: string;
  forceFocus?: boolean;
  isPartOfDiffView: boolean;
  isReadOnly: boolean;
  setContract: (newValue: string) => void;
};

export function listContractOptions(autoCompleteData: AutoCompleteData): string[] {
  const names = Object.keys(autoCompleteData.contractManifests)
    .filter((candidate) => !candidate.startsWith("0x"))
    .map((candidate) =>
      candidate.startsWith("#") ? candidate : `#${candidate}`
    );
  return dedupeAndSort(names);
}

export default function ContractInput({
  autoCompleteData,
  contract,
  forceFocus,
  isPartOfDiffView,
  isReadOnly,
  setContract,
}: Props) {
  const selectRef = useRef<HTMLSelectElement>(null);
  const inputId = useRef(
    `neo-contract-${Math.random().toString(36).slice(2)}`
  ).current;

  useEffect(() => {
    if (forceFocus) {
      selectRef.current?.focus();
    }
  }, [forceFocus]);

  const options = listContractOptions(autoCompleteData);
  const current = contract || "";
  const known = options.includes(current);

  if (options.length) {
    return (
      <div className="neo-field">
        <label className="neo-field__label" htmlFor={inputId}>
          Contract
        </label>
        <select
          className="neo-select"
          disabled={isReadOnly}
          id={inputId}
          ref={selectRef}
          value={known ? current : ""}
          onChange={(event) => setContract(event.target.value)}
        >
          <option value="">Select a contract…</option>
          {options.map((candidate) => (
            <option key={candidate} value={candidate}>
              {candidate.replace(/^#/, "")}
            </option>
          ))}
        </select>
      </div>
    );
  }

  return (
    <div className="neo-field">
      <label className="neo-field__label" htmlFor={inputId}>
        Contract
      </label>
      <input
        className="neo-input"
        disabled={isReadOnly}
        id={inputId}
        type="text"
        value={current}
        onChange={(event) => setContract(event.target.value)}
      />
      {!isPartOfDiffView && (
        <div className="neo-field__meta">
          No contracts found. Build a .nef in the workspace or wait for the
          connected chain to load native contracts.
        </div>
      )}
    </div>
  );
}
