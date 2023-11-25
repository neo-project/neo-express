import React from "react";

import NavButton from "./NavButton";

type Props = {
  affinity?: "top-left" | "middle" | "bottom-right";
  children: any;
  closeButtonText?: string;
  title?: string;
  onClose: () => void;
};

export default function Dialog({
  affinity,
  children,
  closeButtonText,
  title,
  onClose,
}: Props) {
  affinity = affinity || "middle";
  closeButtonText = closeButtonText || "Close";
  return (
    <div
      style={{
        position: "fixed",
        top: 0,
        bottom: 0,
        left: 0,
        right: 0,
        cursor: "pointer",
        display: "flex",
        backgroundColor: "rgba(255,255,255,0.50)",
        justifyContent:
          affinity === "top-left"
            ? "flex-start"
            : affinity === "bottom-right"
            ? "flex-end"
            : "center",
        alignItems:
          affinity === "top-left"
            ? "flex-start"
            : affinity === "bottom-right"
            ? "flex-end"
            : "center",
        zIndex: 100,
      }}
      onClick={onClose}
    >
      <div
        style={{
          cursor: "default",
          backgroundColor: "var(--vscode-editor-background)",
          color: "var(--vscode-editor-foreground)",
          border: "1px solid var(--vscode-focusBorder)",
          boxShadow: "-1px 1px 2px 0px var(--vscode-focusBorder)",
          borderRadius: 10,
          padding: 20,
          margin: 30,
          overflow: "auto",
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-evenly",
          alignItems: "center",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {!!title && (
          <h2 style={{ margin: 0, padding: 0, textAlign: "center" }}>
            {title}
          </h2>
        )}
        <div style={{ margin: 15, maxHeight: "65vh", overflow: "auto" }}>
          {children}
        </div>
        <div>
          <NavButton clickOnEnter onClick={onClose}>
            {closeButtonText}
          </NavButton>
        </div>
      </div>
    </div>
  );
}
