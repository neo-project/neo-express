import React, { useState } from "react";

type Props = {
  i: number;
  dragActive: boolean;
  onDrop: (from: number, to: number) => void;
};

export default function DropTarget({ i, dragActive, onDrop }: Props) {
  const [active, setActive] = useState(false);
  const style: React.CSSProperties = {
    margin: dragActive ? 0 : 10,
    paddingTop: dragActive ? 15 : 0,
    paddingBottom: dragActive ? 15 : 0,
  };
  return (
    <div
      onDragOver={(e) => {
        e.preventDefault();
        setActive(true);
      }}
      onDragLeave={() => {
        setActive(false);
      }}
      onDrop={(e) => {
        e.preventDefault();
        const data = e.dataTransfer.getData("InvocationStep");
        if (data) {
          const from = parseInt(data);
          const to = i;
          if (from !== to) {
            onDrop(from, to);
          }
        }
        setActive(false);
      }}
      style={style}
    >
      <div
        style={{
          backgroundColor: active
            ? "var(--vscode-button-background)"
            : "var(--vscode-editorWidget-background)",
          height: dragActive ? 5 : 0,
        }}
      ></div>
    </div>
  );
}
