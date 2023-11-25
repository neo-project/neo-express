import React from "react";
import ReactDOM from "react-dom";

import ViewRouter from "./viewRouter";

import "./index.html";

function initialize() {
  (window.document.querySelector("html") as HTMLElement).style.height = "100%";
  (window.document.querySelector("#root") as HTMLElement).style.height = "100%";
  window.document.body.style.height = "100%";
  const negateVsCodeMargin: React.CSSProperties = {
    margin: "0 -20px",
    backgroundColor: "var(--vscode-sideBar-background)",
    height: "100%",
  };
  ReactDOM.render(
    <React.StrictMode>
      <div style={negateVsCodeMargin}>
        <ViewRouter />
      </div>
    </React.StrictMode>,
    document.getElementById("root")
  );
}

window.onload = initialize;
