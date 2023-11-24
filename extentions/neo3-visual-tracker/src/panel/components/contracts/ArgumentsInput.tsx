import React from "react";

import * as neonSc from "@cityofzion/neon-core/lib/sc";

import ArgumentInput from "./ArgumentInput";

type Props = {
  args: any[];
  autoSuggestListId: string;
  isReadOnly: boolean;
  parameterDefinitions?: neonSc.ContractParameterDefinitionJson[];
  style?: React.CSSProperties;
  setArguments: (newArguments: any[]) => void;
};

export default function ArgumentsInput({
  args,
  autoSuggestListId,
  isReadOnly,
  parameterDefinitions,
  style,
  setArguments,
}: Props) {
  while (args.length && !args[args.length - 1]) {
    args.length--;
  }
  while (args.length < (parameterDefinitions?.length || 0)) {
    args.push("");
  }
  return (
    <div style={style}>
      {(!parameterDefinitions || !!args.length) && (
        <div>
          <strong>Arguments:</strong>
        </div>
      )}
      {args.map((_, i) => (
        <ArgumentInput
          arg={_}
          autoSuggestListId={autoSuggestListId}
          isReadOnly={isReadOnly}
          key={`${i}_${_}`}
          name={(parameterDefinitions || [])[i]?.name || `Argument #${i + 1}`}
          type={(parameterDefinitions || [])[i]?.type}
          onUpdate={(arg) =>
            setArguments(
              args
                .map((__, j) => (i === j ? arg : __))
                .filter(
                  (__, j) => !!__ || j < (parameterDefinitions?.length || 0)
                )
            )
          }
        />
      ))}
      {!parameterDefinitions && (
        <ArgumentInput
          autoSuggestListId={autoSuggestListId}
          isReadOnly={isReadOnly}
          key={args.length}
          name={`Argument #${args.length + 1}`}
          onUpdate={(arg) => setArguments(arg ? [...args, arg] : [...args])}
        />
      )}
    </div>
  );
}
