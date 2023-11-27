import React, { useState, useEffect, useRef } from "react";

type Props = {
  children: JSX.Element | string;
  clickOnEnter?: boolean;
  roundedBadge?: boolean;
  disabled?: boolean;
  style?: React.CSSProperties;
  title?: string;
  onClick: () => void;
};

export default function NavButton({
  children,
  clickOnEnter,
  roundedBadge,
  disabled,
  style,
  title,
  onClick,
}: Props) {
  const [hover, setHover] = useState(false);
  useEffect(() => setHover(disabled ? false : hover), [disabled]);
  const buttonRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    if (clickOnEnter) {
      buttonRef.current?.focus();
    } else {
      buttonRef.current?.blur();
    }
  });
  const buttonStyle: React.CSSProperties = {
    backgroundColor: disabled
      ? "var(--vscode-button-secondaryBackground)"
      : "var(--vscode-button-background)",
    color: disabled
      ? "var(--vscode-button-secondaryForeground)"
      : "var(--vscode-button-foreground)",
    border: "none",
    padding: roundedBadge ? "5px 10px" : "1em 2em 1em 2em",
    borderRadius: roundedBadge ? 10 : undefined,
  };
  const buttonStyleHover: React.CSSProperties = {
    ...buttonStyle,
    backgroundColor: "var(--vscode-button-hoverBackground)",
    cursor: "pointer",
  };
  return (
    <span style={style}>
      <button
        type="button"
        style={hover && !disabled ? buttonStyleHover : buttonStyle}
        disabled={!!disabled}
        onClick={(e) => {
          if (roundedBadge) {
            (e.target as HTMLButtonElement).blur();
          }
          onClick();
        }}
        onMouseMove={() => setHover(true)}
        onMouseLeave={() => setHover(false)}
        ref={buttonRef}
        title={title}
      >
        {children}
      </button>
    </span>
  );
}
