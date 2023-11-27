import React from "react";

import * as neonSc from "@cityofzion/neon-core/lib/sc";

import ArgumentsInput from "./ArgumentsInput";
import AutoCompleteData from "../../../shared/autoCompleteData";
import ContractInput from "./ContractInput";
import OperationInput from "./OperationInput";
import NavButton from "../NavButton";

type Props = {
  i: number;
  contract?: string;
  operation?: string;
  args?: any[];
  autoCompleteData: AutoCompleteData;
  argumentSuggestionListId: string;
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
  let canRun = false;
  let canDebug = false;
  if (contract) {
    let contractHashOrName = contract;
    if (contractHashOrName.startsWith("#")) {
      contractHashOrName = contractHashOrName.substring(1);
    }
    let manifest = autoCompleteData.contractManifests[contractHashOrName];
    if (!manifest) {
      for (const contractHash of Object.keys(autoCompleteData.contractNames)) {
        const contractName = autoCompleteData.contractNames[contractHash];
        if (contractName === contractHashOrName) {
          manifest = autoCompleteData.contractManifests[contractHash];
        }
      }
    }
    if (operation) {
      canRun = true;
      const paths = autoCompleteData.contractPaths[contractHashOrName] || [];
      canDebug = paths.length > 0;
    }
    if (manifest?.abi) {
      operations = manifest.abi.methods;
    }
  }
  return (
    <div
      draggable={!isReadOnly}
      onDragStart={
        isReadOnly
          ? undefined
          : (e) => {
              e.dataTransfer.setData("InvocationStep", `${i}`);
              onDragStart();
            }
      }
      onDragEnd={isReadOnly ? undefined : onDragEnd}
      style={{
        backgroundColor: "var(--vscode-editorWidget-background)",
        color: "var(--vscode-editorWidget-foreground)",
        border: "var(--vscode-editorWidget-border)",
        borderRadius: 10,
        marginLeft: 10,
        marginRight: 10,
        padding: 15,
        cursor: isReadOnly ? undefined : "move",
      }}
    >
      <ContractInput
        autoCompleteData={autoCompleteData}
        contract={contract}
        forceFocus={forceFocus}
        isPartOfDiffView={isPartOfDiffView}
        isReadOnly={isReadOnly}
        style={{ marginBottom: 10 }}
        setContract={(newContract: string) =>
          onUpdate(newContract, operation, args)
        }
      />
      <OperationInput
        isReadOnly={isReadOnly}
        operations={operations}
        operation={operation}
        style={{ marginBottom: 10 }}
        setOperation={(newOperation: string) =>
          onUpdate(contract, newOperation, args)
        }
      />
      <ArgumentsInput
        args={args || []}
        autoSuggestListId={argumentSuggestionListId}
        isReadOnly={isReadOnly}
        parameterDefinitions={
          operations.find((_) => _.name === operation)?.parameters
        }
        style={{ marginBottom: 10 }}
        setArguments={(newArguments) =>
          onUpdate(contract, operation, newArguments)
        }
      />
      {(!isReadOnly || isPartOfDiffView) && (
        <div style={{ textAlign: "right" }}>
          <NavButton
            onClick={onDelete}
            disabled={isReadOnly}
            style={{
              visibility: isPartOfDiffView && isReadOnly ? "hidden" : undefined,
            }}
          >
            Delete this step
          </NavButton>
          {!isPartOfDiffView && (
            <>
              {" "}
              <NavButton
                onClick={onRun}
                disabled={isReadOnly || !canRun}
                title={
                  canRun
                    ? undefined
                    : "You must at least specify a contract and an operaton name."
                }
              >
                Run this step
              </NavButton>
            </>
          )}
          {!isPartOfDiffView && (
            <>
              {" "}
              <NavButton
                onClick={onDebug}
                disabled={isReadOnly || !canDebug}
                title={
                  canDebug
                    ? undefined
                    : "To debug, the contract source code must be in the current workspace and the contract must be built."
                }
              >
                Debug this step
              </NavButton>
            </>
          )}
        </div>
      )}
    </div>
  );
}
