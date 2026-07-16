import React from "react";

import * as neonSc from "@cityofzion/neon-core/lib/sc";

import ArgumentsInput from "./ArgumentsInput";
import AutoCompleteData from "../../../shared/autoCompleteData";
import ContractInput from "./ContractInput";
import NavButton from "../NavButton";
import OperationInput from "./OperationInput";

type Props = {
  i: number;
  contract?: string;
  operation?: string;
  args?: any[];
  autoCompleteData: AutoCompleteData;
  argumentSuggestionListId: string;
  executionReadinessMessage: string;
  executionReady: boolean;
  forceFocus?: boolean;
  isPartOfDiffView: boolean;
  isReadOnly: boolean;
  onDebug: () => void;
  onDelete: () => void;
  onDragStart: () => void;
  onDragEnd: () => void;
  onRun: () => void;
  onUpdate: (contract?: string, operation?: string, args?: any[]) => void;
};

export default function InvocationStep({
  i,
  contract,
  operation,
  args,
  autoCompleteData,
  argumentSuggestionListId,
  executionReadinessMessage,
  executionReady,
  forceFocus,
  isPartOfDiffView,
  isReadOnly,
  onDebug,
  onRun,
  onDelete,
  onDragStart,
  onDragEnd,
  onUpdate,
}: Props) {
  let operations: neonSc.ContractMethodDefinitionJson[] = [];
  let canDebug = false;
  if (contract) {
    let contractHashOrName = contract;
    if (contractHashOrName.startsWith("#")) {
      contractHashOrName = contractHashOrName.substring(1);
    }
    let manifest = autoCompleteData.contractManifests[contractHashOrName];
    if (!manifest) {
      for (const contractHash of Object.keys(autoCompleteData.contractNames)) {
        if (autoCompleteData.contractNames[contractHash] === contractHashOrName) {
          manifest = autoCompleteData.contractManifests[contractHash];
        }
      }
    }
    const paths = autoCompleteData.contractPaths[contractHashOrName] || [];
    canDebug = !!operation && paths.length > 0;
    if (manifest?.abi) {
      operations = manifest.abi.methods;
    }
  }

  const canRun = !!contract && !!operation && executionReady;
  const runTitle = !contract || !operation
    ? "Select a contract and method before running this invocation."
    : executionReadinessMessage;

  return (
    <section
      aria-labelledby={`invocation-step-${i}`}
      className="invocation-step"
      draggable={!isReadOnly}
      onDragEnd={isReadOnly ? undefined : onDragEnd}
      onDragStart={
        isReadOnly
          ? undefined
          : (event) => {
              event.dataTransfer.setData("InvocationStep", `${i}`);
              onDragStart();
            }
      }
    >
      <header className="invocation-step__header">
        <div>
          <div className="invocation-step__eyebrow">Invocation {i + 1}</div>
          <h2 className="invocation-step__title" id={`invocation-step-${i}`}>
            {operation || "Configure contract method"}
          </h2>
        </div>
        {!isReadOnly && (
          <NavButton
            ariaLabel={`Delete invocation ${i + 1}`}
            icon="trash"
            iconOnly
            onClick={onDelete}
            title="Delete invocation"
            variant="ghost"
          />
        )}
      </header>

      <div className="invocation-step__fields">
        <ContractInput
          autoCompleteData={autoCompleteData}
          contract={contract}
          forceFocus={forceFocus}
          isPartOfDiffView={isPartOfDiffView}
          isReadOnly={isReadOnly}
          setContract={(newContract) => onUpdate(newContract, operation, args)}
        />
        <OperationInput
          isReadOnly={isReadOnly}
          operations={operations}
          operation={operation}
          setOperation={(newOperation) =>
            onUpdate(contract, newOperation, args)
          }
        />
        <ArgumentsInput
          args={args || []}
          autoSuggestListId={argumentSuggestionListId}
          isReadOnly={isReadOnly}
          parameterDefinitions={
            operations.find((candidate) => candidate.name === operation)
              ?.parameters
          }
          setArguments={(newArguments) =>
            onUpdate(contract, operation, newArguments)
          }
        />
      </div>

      {!isReadOnly && !isPartOfDiffView && (
        <div className="invocation-step__actions">
          <span className="invocation-step__hint">
            {canRun
              ? "Ready to sign and relay"
              : runTitle}
          </span>
          <NavButton
            disabled={!canDebug}
            icon="debug-alt"
            onClick={onDebug}
            title={
              canDebug
                ? "Start a source debug session"
                : "Build this contract in the current workspace to enable debugging."
            }
            variant="secondary"
          >
            Debug
          </NavButton>
          <NavButton
            disabled={!canRun}
            icon="play"
            onClick={onRun}
            title={runTitle}
          >
            Run invocation
          </NavButton>
        </div>
      )}
    </section>
  );
}
