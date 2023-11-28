import React from "react";

export default function LoadingIndicator() {
  // Inspiration for CSS-only loading spinner from: https://loading.io/css/

  const outerStyle: React.CSSProperties = {
    display: "inline-block",
    position: "relative",
    width: 40,
    height: 40,
  };

  const innerStyle: React.CSSProperties = {
    boxSizing: "border-box",
    display: "block",
    position: "absolute",
    width: 32,
    height: 32,
    margin: 4,
    border: "4px solid",
    borderRadius: "50%",
    animation: "lds-ring 1.2s cubic-bezier(0.5, 0, 0.5, 1) infinite",
    borderColor:
      "var(--vscode-statusBar-background) transparent transparent transparent",
  };

  return (
    <div
      style={{
        position: "fixed",
        bottom: 0,
        right: 0,
        marginBottom: 20,
        marginRight: 40,
        zIndex: 10000,
      }}
    >
      <style type="text/css">
        {`
          @keyframes lds-ring {
            0% {
              transform: rotate(0deg);
            }
            100% {
              transform: rotate(360deg);
            }
          }
        `}
      </style>
      <div style={outerStyle}>
        <div style={{ ...innerStyle, animationDelay: "-0.45s" }}></div>
        <div style={{ ...innerStyle, animationDelay: "-0.3s" }}></div>
        <div style={{ ...innerStyle, animationDelay: "-0.15s" }}></div>
        <div style={{ ...innerStyle }}></div>
      </div>
    </div>
  );
}
