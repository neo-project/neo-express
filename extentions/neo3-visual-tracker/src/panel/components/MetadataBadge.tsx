import React from "react";

type Props = {
  alignLeft?: boolean;
  children: any;
  grow?: boolean;
  title: string;
};

export default function MetadataBadge({
  alignLeft,
  children,
  grow,
  title,
}: Props) {
  const style: React.CSSProperties = {
    textAlign: alignLeft ? "left" : "center",
    margin: 5,
  };
  const titleStyle: React.CSSProperties = {
    textAlign: "center",
    fontWeight: "bold",
    fontSize: "0.6rem",
    textTransform: "uppercase",
    marginBottom: 2,
  };
  const badgeStyle: React.CSSProperties = {
    backgroundColor: "var(--vscode-badge-background)",
    color: "var(--vscode-badge-foreground)",
    borderRadius: 10,
    padding: 5,
    maxHeight: grow ? undefined : "3em",
    overflow: grow ? undefined : "auto",
  };
  return (
    <div style={style}>
      <div style={titleStyle}>{title}:</div>
      <div style={badgeStyle}>{children}</div>
    </div>
  );
}
