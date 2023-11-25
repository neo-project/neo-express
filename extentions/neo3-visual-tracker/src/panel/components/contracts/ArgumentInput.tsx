import React, { useState } from "react";

import InputNonDraggable from "../InputNonDraggable";
import NeoType from "./NeoType";

type Props = {
  arg?: any;
  autoSuggestListId: string;
  isReadOnly: boolean;
  name: string;
  type?: string | number;
  onUpdate: (newArgument: any) => void;
};

const valueToString = (value: any) => {
  if (!value) {
    return "";
  } else if (Array.isArray(value) || typeof value === "object") {
    return JSON.stringify(value);
  } else {
    return `${value}`;
  }
};

const stringToValue = (text: string) => {
  if (`${parseInt(text)}` === text) {
    return parseInt(text);
  } else if (`${parseFloat(text)}` === text) {
    return parseFloat(text);
  } else {
    try {
      return JSON.parse(text);
    } catch (e) {
      return `${text}`;
    }
  }
};

export default function ArgumentInput({
  arg,
  autoSuggestListId,
  isReadOnly,
  name,
  type,
  onUpdate,
}: Props) {
  const [value, setValue] = useState(valueToString(arg));
  const inputStyle: React.CSSProperties = {
    color: "var(--vscode-input-foreground)",
    backgroundColor: "var(--vscode-input-background)",
    border: "1px solid var(--vscode-input-border)",
    boxSizing: "border-box",
    width: "calc(100% - 15px)",
    fontSize: "0.8rem",
    padding: 1,
    marginLeft: 15,
  };
  return (
    <div style={{ marginLeft: 15, marginTop: 4 }}>
      <div>
        <strong>{name}</strong>{" "}
        <small>
          {" "}
          <em>
            (<NeoType type={type} />)
          </em>
        </small>
      </div>
      <InputNonDraggable
        disabled={isReadOnly}
        list={autoSuggestListId}
        style={inputStyle}
        type="text"
        value={value}
        onBlur={(e) => onUpdate(stringToValue(e.target.value))}
        onChange={(e) => setValue(valueToString(e.target.value))}
        onKeyDown={(e) => {
          if (e.metaKey) {
            // User may be about to save
            onUpdate(stringToValue(value));
          }
        }}
      />
    </div>
  );
}
