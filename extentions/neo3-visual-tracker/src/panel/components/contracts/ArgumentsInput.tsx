import React from "react";

import * as neonSc from "@cityofzion/neon-core/lib/sc";

import {
  isEmptyArgument,
  normalizeArguments,
} from "../../../shared/argumentValues";
import ArgumentInput from "./ArgumentInput";

type Props = {
  args: any[];
  autoSuggestListId: string;
  isReadOnly: boolean;
  parameterDefinitions?: neonSc.ContractParameterDefinitionJson[];
  setArguments: (newArguments: any[]) => void;
};

export default function ArgumentsInput({
  args,
  autoSuggestListId,
  isReadOnly,
  parameterDefinitions,
  setArguments,
}: Props) {
  const requiredArgumentCount = parameterDefinitions?.length || 0;
  const normalizedArguments = normalizeArguments(args, requiredArgumentCount);
  return (
    <div className="neo-field">
      {(!parameterDefinitions || !!normalizedArguments.length) && (
        <div className="neo-field__label">Arguments</div>
      )}
      <div className="argument-list">
        {normalizedArguments.map((argument, i) => (
          <ArgumentInput
            arg={argument}
            autoSuggestListId={autoSuggestListId}
            isReadOnly={isReadOnly}
            key={`${i}_${JSON.stringify(argument)}`}
            name={
              (parameterDefinitions || [])[i]?.name || `Argument ${i + 1}`
            }
            type={(parameterDefinitions || [])[i]?.type}
            onUpdate={(updatedArgument) =>
              setArguments(
                normalizeArguments(
                  normalizedArguments.map((candidate, j) =>
                    i === j ? updatedArgument : candidate
                  ),
                  requiredArgumentCount
                )
              )
            }
          />
        ))}
        {!parameterDefinitions && (
          <ArgumentInput
            autoSuggestListId={autoSuggestListId}
            isReadOnly={isReadOnly}
            key={normalizedArguments.length}
            name={`Argument ${normalizedArguments.length + 1}`}
            onUpdate={(argument) =>
              setArguments(
                !isEmptyArgument(argument)
                  ? [...normalizedArguments, argument]
                  : [...normalizedArguments]
              )
            }
          />
        )}
      </div>
    </div>
  );
}
