import React from "react";

import AutoCompleteData from "../../../shared/autoCompleteData";
import Dialog from "../Dialog";
import Hash from "../Hash";
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
    (_) => _.txid === selectedTransactionId
  );
  return (
    <div>
      {!!selectedEntry?.tx && (
        <Dialog title="Transaction" onClose={() => onSelectTransaction(null)}>
          <TransactionDetails
            applicationLog={selectedEntry.log}
            autoCompleteData={autoCompleteData}
            transaction={selectedEntry.tx}
          />
        </Dialog>
      )}
      <div
        style={{
          paddingBottom: 15,
          color: "var(--vscode-panelTitle-inactiveForeground)",
        }}
      >
        TRANSACTIONS
      </div>
      {transactions.map((entry) => (
        <div
          key={entry.txid}
          style={{
            marginBottom: 10,
            backgroundColor: "var(--vscode-editorWidget-background)",
            color: "var(--vscode-editorWidget-foreground)",
            border: "var(--vscode-editorWidget-border)",
            borderRadius: 10,
            padding: 10,
            cursor: entry.tx ? "pointer" : undefined,
          }}
          onClick={entry.tx ? () => onSelectTransaction(entry.txid) : undefined}
        >
          <div
            style={{
              color: "var(--vscode-panelTitle-inactiveForeground)",
              fontWeight: "bold",
            }}
          >
            {entry.blockchain}
            <span style={{ float: "right" }}>{entry.state}</span>
          </div>
          <div
            style={{
              paddingTop: 10,
              fontSize: "1.1rem",
            }}
          >
            <Hash hash={entry.txid} />
          </div>
        </div>
      ))}
      {!transactions.length && (
        <>As you run the steps in your file, results will appear here.</>
      )}
    </div>
  );
}
