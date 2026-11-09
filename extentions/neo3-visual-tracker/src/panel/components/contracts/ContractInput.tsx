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
  setContract: (newValue: string) => void;
};

export default function ContractInput({
  autoCompleteData,
  contract,
  forceFocus,
  isPartOfDiffView,
  isReadOnly,
  setContract,
}: Props) {
  const inputRef = useRef<HTMLInputElement>(null);
  const inputId = useRef(
    `neo-contract-${Math.random().toString(36).slice(2)}`
  ).current;
  const [hasFocus, setHasFocus] = useState(false);

  useEffect(() => {
    if (forceFocus) {
      inputRef.current?.focus();
    }
  }, [forceFocus]);

  const allNamesAndHashes = dedupeAndSort(
    Object.keys(autoCompleteData.contractManifests).map((candidate) =>
      candidate.startsWith("0x")
        ? autoCompleteData.contractNames[candidate] || candidate
        : candidate
    )
  );
  let contractHashOrName = contract || "";
  if (contractHashOrName.startsWith("#")) {
    contractHashOrName = contractHashOrName.substring(1);
  }
  const alternateName =
    autoCompleteData.contractNames[contractHashOrName] || "";

  return (
    <div className="neo-field neo-combobox">
      <label className="neo-field__label" htmlFor={inputId}>
        Contract
      </label>
      <InputNonDraggable
        className="neo-input"
        disabled={isReadOnly}
        id={inputId}
        inputRef={inputRef}
        type="text"
        value={contract || ""}
        onBlur={() => setHasFocus(false)}
        onChange={(event) => setContract(event.target.value)}
        onFocus={() => setHasFocus(true)}
      />
      {hasFocus && !!allNamesAndHashes.length && (
        <div className="neo-combobox__menu">
          {allNamesAndHashes.map((candidate) => (
            <ContractTile
              key={candidate}
              contractHashOrName={candidate}
              autoCompleteData={autoCompleteData}
              onMouseDown={setContract}
            />
          ))}
        </div>
      )}
      {!isPartOfDiffView && !!alternateName && (
        <div className="neo-field__meta">
          Alias:{" "}
          <button
            className="neo-inline-action"
            onClick={() => setContract(alternateName)}
            type="button"
          >
            {alternateName}
          </button>
        </div>
      )}
    </div>
  );
}
