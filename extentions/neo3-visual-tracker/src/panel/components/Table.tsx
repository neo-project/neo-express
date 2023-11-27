import React, { Fragment } from "react";

import useWindowHeight from "./useWindowHeight";

function sum(ns: number[]) {
  let result = 0;
  for (const n of ns) {
    result += n;
  }
  return result;
}

type Props = {
  headings?: { key?: string; content: JSX.Element }[];
  rows: {
    key?: string;
    parity?: boolean;
    onClick?: () => void;
    cells: { key?: string; colSpan?: number; content: JSX.Element }[];
    annotation?: JSX.Element;
    selected?: boolean;
  }[];
};

export default function Table({ headings, rows }: Props) {
  const windowHeight = useWindowHeight();
  const tableStyle: React.CSSProperties = {
    height: "100%",
    width: "100%",
    borderCollapse: "collapse",
    border: "1px solid var(--vscode-editor-lineHighlightBorder)",
  };
  const theadStyle: React.CSSProperties = {
    backgroundColor: "var(--vscode-editor-selectionBackground)",
    color: "var(--vscode-editor-selectionForeground)",
    textTransform: "uppercase",
    fontSize: "0.6rem",
  };
  const trStyleEven: React.CSSProperties = {
    borderBottom: "1px solid var(--vscode-editor-lineHighlightBorder)",
    backgroundColor: "var(--vscode-editor-background)",
    color: "var(--vscode-editor-foreground)",
  };
  const trStyleOdd: React.CSSProperties = {
    borderBottom: "1px solid var(--vscode-editor-lineHighlightBorder)",
    backgroundColor: "var(--vscode-editor-inactiveSelectionBackground)",
    color: "var(--vscode-editor-foreground)",
  };
  const cellStyle: React.CSSProperties = {
    textAlign: "center",
    padding: 5,
  };
  const insetStyleOuter: React.CSSProperties = {
    backgroundColor: "var(--vscode-editor-background)",
    color: "var(--vscode-editor-foreground)",
    margin: "-1px 5% 10px 5%",
    borderRadius: "0px 0px 15px 15px",
    padding: 10,
    borderBottom: "1px solid var(--vscode-editor-lineHighlightBorder)",
    borderLeft: "1px solid var(--vscode-editor-lineHighlightBorder)",
    borderRight: "1px solid var(--vscode-editor-lineHighlightBorder)",
  };
  const insetStyleInner: React.CSSProperties = {
    overflow: "scroll",
    maxHeight: windowHeight - 200,
  };
  return (
    <table style={tableStyle}>
      {!!headings && (
        <thead style={theadStyle}>
          <tr>
            {headings.map((heading, i) => (
              <th style={cellStyle} key={heading.key || i}>
                {heading.content}
              </th>
            ))}
          </tr>
        </thead>
      )}
      <tbody>
        {rows.map((row, i) => (
          <Fragment key={row.key || undefined}>
            <tr
              style={{
                ...(row.parity !== undefined
                  ? row.parity
                    ? trStyleEven
                    : trStyleOdd
                  : i % 2 === 0
                  ? trStyleEven
                  : trStyleOdd),
                cursor: row.onClick ? "pointer" : undefined,
                fontWeight: row.selected ? "bold" : undefined,
              }}
              onClick={row.onClick}
            >
              {row.cells.map((cell, i) => (
                <td
                  key={cell.key || i}
                  style={cellStyle}
                  colSpan={cell.colSpan}
                >
                  {cell.content}
                </td>
              ))}
            </tr>
            {!!row.annotation && (
              <tr>
                <td colSpan={sum(row.cells.map((_) => _.colSpan || 1))}>
                  <div style={insetStyleOuter}>
                    <div style={insetStyleInner}>{row.annotation}</div>
                  </div>
                </td>
              </tr>
            )}
          </Fragment>
        ))}
      </tbody>
    </table>
  );
}
