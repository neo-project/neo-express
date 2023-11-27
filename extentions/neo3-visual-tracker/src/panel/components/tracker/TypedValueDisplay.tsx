import React from "react";
import * as buffer from "buffer";

import AutoCompleteData from "../../../shared/autoCompleteData";
import ScriptToken from "./ScriptToken";
import TypedValue from "../../../shared/typedValue";

type Props = {
  autoCompleteData: AutoCompleteData;
  value: TypedValue;
  selectAddress?: (address: string) => void;
};

export default function TypedValueDisplay({
  autoCompleteData,
  value,
  selectAddress,
}: Props) {
  if (Array.isArray(value.value)) {
    return (
      <ul style={{ margin: 0 }}>
        {value.value.map((_, i) => (
          <li key={i}>
            <TypedValueDisplay
              autoCompleteData={autoCompleteData}
              value={_}
              selectAddress={selectAddress}
            />
          </li>
        ))}
      </ul>
    );
  } else if (value.type === "ByteString") {
    return (
      <ScriptToken
        autoCompleteData={autoCompleteData}
        token={buffer.Buffer.from(`${value.value}`, "base64").toString("hex")}
        selectAddress={selectAddress}
      />
    );
  } else if (value.type === "Boolean") {
    return <>{value.value === true ? "True" : "False"}</>;
  } else if (value.type === "Integer") {
    return <>{parseInt(`${value.value}`)}</>;
  } else if (!value.value) {
    return <i>(null)</i>;
  } else {
    return <>{JSON.stringify(value)}</>;
  }
}
