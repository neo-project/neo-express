import React, { useEffect, useRef, useState } from "react";

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
  const inputId = useRef(
    `neo-argument-${Math.random().toString(36).slice(2)}`
  ).current;
  useEffect(() => setValue(valueToString(arg)), [arg]);
  return (
    <div className="argument-field">
      <label className="argument-field__name" htmlFor={inputId}>
        <span>{name}</span>
        <span className="argument-field__type">
          <NeoType type={type} />
        </span>
      </label>
      <InputNonDraggable
        className="neo-input"
        disabled={isReadOnly}
        id={inputId}
        list={autoSuggestListId}
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
