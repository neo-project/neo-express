import React from "react";

import AutoCompleteData from "../../../shared/autoCompleteData";
import disassembleScript from "./disassembleScript";
import ScriptToken from "./ScriptToken";

const tryDisassemble = (script: string) => {
  try {
    return disassembleScript(script) || script;
  } catch {
    return script;
  }
};

const tokenizeScript = (script: string) => {
  return script
    .split("\n")
    .map((_) => _.trim())
    .filter((_) => _.length > 0)
    .map((_) => _.split(/\s+/g));
};

type Props = {
  autoCompleteData: AutoCompleteData;
  script: string;
  selectAddress?: (address: string) => void;
};

export default function Script({
  autoCompleteData,
  script,
  selectAddress,
}: Props) {
  const style: React.CSSProperties = {
    fontFamily: "monospace",
    wordBreak: "break-all",
  };
  const scriptLines = tokenizeScript(tryDisassemble(script));
  return (
    <div style={style}>
      {scriptLines.map((lineTokens, i) => (
        <div key={`${i}.${lineTokens.join(".")}`}>
          {lineTokens.map((_, i) => (
            <ScriptToken
              autoCompleteData={autoCompleteData}
              key={`${i}.${_}`}
              token={_}
              selectAddress={selectAddress}
            />
          ))}
        </div>
      ))}
    </div>
  );
}
