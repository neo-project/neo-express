import React, { useEffect, useRef } from "react";

type Props = {
  fileContentsJson: string;
  isReadOnly: boolean;
  onUpdate?: (newJson: string) => void;
};

export default function InvokeFileJsonEditor({
  fileContentsJson,
  isReadOnly,
  onUpdate,
}: Props) {
  const divRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (divRef.current && divRef.current.innerText !== fileContentsJson) {
      divRef.current.innerText = fileContentsJson;
    }
  }, [fileContentsJson]);
  return (
    <div
      contentEditable={!isReadOnly}
      ref={divRef}
      style={{
        border: 0,
        outline: 0,
        fontFamily: "var(--vscode-editor-font-family)",
        fontWeight: "var(--vscode-editor-font-weight)" as any,
        fontSize: "var(--vscode-editor-font-size)",
        whiteSpace: "pre-wrap",
        padding: 10,
        margin: 0,
      }}
      onKeyUp={() => {
        if (onUpdate) {
          const updateJson = divRef.current?.innerText;
          if (updateJson !== undefined && updateJson != fileContentsJson) {
            onUpdate(updateJson);
          }
        }
      }}
    ></div>
  );
}
