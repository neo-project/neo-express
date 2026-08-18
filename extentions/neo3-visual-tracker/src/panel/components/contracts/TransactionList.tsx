import React from "react";

import AutoCompleteData from "../../../shared/autoCompleteData";
import Dialog from "../Dialog";
import RecentTransaction from "../../../shared/recentTransaction";
import TransactionDetails from "../tracker/TransactionDetails";

type Props = {
  autoCompleteData: AutoCompleteData;
  transactions: RecentTransaction[];
  selectedTransactionId: string | null;
  onSelectTransaction: (txid: string | null) => void;
};

export default function TransactionList({
  autoCompleteData,
  transactions,
  selectedTransactionId,
  onSelectTransaction,
}: Props) {
  const selectedEntry = transactions.find(
    (transaction) => transaction.txid === selectedTransactionId
  );
  return (
    <div className="transaction-panel">
      {!!selectedEntry?.tx && (
        <Dialog
          title="Transaction receipt"
          onClose={() => onSelectTransaction(null)}
        >
          <TransactionDetails
            applicationLog={selectedEntry.log}
            autoCompleteData={autoCompleteData}
            transaction={selectedEntry.tx}
          />
        </Dialog>
      )}
      <header className="transaction-panel__header">
        <h2 className="transaction-panel__title">Recent transactions</h2>
        <span aria-label={`${transactions.length} transactions`}>
          {transactions.length}
        </span>
      </header>
      <ol className="transaction-list">
        {transactions.map((entry) => (
          <li key={entry.txid}>
            <button
              aria-label={`${entry.operation || "Invocation"}, ${entry.state}`}
              className="transaction-row"
              disabled={!entry.tx}
              onClick={() => onSelectTransaction(entry.txid)}
              type="button"
            >
              <span className="transaction-row__top">
                <span className="transaction-row__operation">
                  {entry.operation || "Invocation"}
                </span>
                <span
                  className={`transaction-status transaction-status--${entry.state}`}
                >
                  <i
                    aria-hidden="true"
                    className={`codicon codicon-${
                      entry.state === "confirmed" ? "pass-filled" : "loading"
                    }`}
                  />
                  {entry.state}
                </span>
              </span>
              <span className="transaction-row__hash">
                {shortenHash(entry.txid)}
              </span>
              <span className="transaction-row__bottom">
                <span>{entry.account || entry.blockchain}</span>
                <span>{formatSubmittedAt(entry.submittedAt)}</span>
              </span>
            </button>
          </li>
        ))}
      </ol>
      {!transactions.length && (
        <div className="transaction-panel__empty">
          Run an invocation to see its submission and confirmation status here.
        </div>
      )}
    </div>
  );
}

function shortenHash(hash: string) {
  if (hash.length <= 24) {
    return hash;
  }
  return `${hash.slice(0, 12)}...${hash.slice(-8)}`;
}

function formatSubmittedAt(submittedAt?: string) {
  if (!submittedAt) {
    return "";
  }
  const date = new Date(submittedAt);
  return Number.isNaN(date.getTime())
    ? ""
    : date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}
