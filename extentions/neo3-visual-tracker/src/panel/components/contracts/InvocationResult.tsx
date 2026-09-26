import React from "react";

import { InvocationResult } from "../../../shared/viewState/invokeFileViewState";

type Props = {
  result: InvocationResult;
};

export default function InvocationResultView({ result }: Props) {
  return (
    <section
      aria-label="Invocation result"
      className={`invocation-result invocation-result--${
        result.success ? "success" : "error"
      }`}
    >
      <header className="invocation-result__header">
        <h2 className="invocation-result__title">
          {result.success ? "Invocation result" : "Invocation failed"}
        </h2>
        <span className="invocation-result__meta">
          {result.operation || "Invocation"}
          {result.submittedAt ? ` · ${formatTime(result.submittedAt)}` : ""}
        </span>
      </header>
      <pre className="invocation-result__body">{result.message}</pre>
      {!!result.txids.length && (
        <div className="invocation-result__txids">
          {result.txids.map((txid) => (
            <div key={txid}>{txid}</div>
          ))}
        </div>
      )}
    </section>
  );
}

function formatTime(submittedAt: string) {
  const date = new Date(submittedAt);
  return Number.isNaN(date.getTime())
    ? ""
    : date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}
