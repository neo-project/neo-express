import React from "react";
import ReactDOM from "react-dom";

import "@vscode/codicons/dist/codicon.css";
import "./styles.css";

import ViewRouter from "./viewRouter";

import "./index.html";

function initialize() {
  ReactDOM.render(
    <React.StrictMode>
      <div className="panel-shell">
        <ViewRouter />
      </div>
    </React.StrictMode>,
    document.getElementById("root")
  );
}

window.onload = initialize;
