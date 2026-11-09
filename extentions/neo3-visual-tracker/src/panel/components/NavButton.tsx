import React, { useEffect, useRef } from "react";

type Props = {
  children?: React.ReactNode;
  ariaLabel?: string;
  clickOnEnter?: boolean;
  className?: string;
  roundedBadge?: boolean;
  disabled?: boolean;
  icon?: string;
  iconOnly?: boolean;
  style?: React.CSSProperties;
  title?: string;
  variant?: "primary" | "secondary" | "ghost" | "danger";
  onClick: () => void;
};

export default function NavButton({
  children,
  ariaLabel,
  clickOnEnter,
  className,
  roundedBadge,
  disabled,
  icon,
  iconOnly,
  style,
  title,
  variant = "primary",
  onClick,
}: Props) {
  const buttonRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    if (clickOnEnter) {
      buttonRef.current?.focus();
    }
  }, [clickOnEnter]);
  const classes = [
    "neo-button",
    `neo-button--${variant}`,
    roundedBadge ? "neo-button--badge" : "",
    iconOnly ? "neo-button--icon" : "",
    className || "",
  ]
    .filter(Boolean)
    .join(" ");
  return (
    <span className="neo-button-wrap" style={style}>
      <button
        aria-label={
          ariaLabel ||
          (iconOnly && typeof title === "string" ? title : undefined)
        }
        className={classes}
        type="button"
        disabled={!!disabled}
        onClick={(e) => {
          if (roundedBadge) {
            e.currentTarget.blur();
          }
          onClick();
        }}
        ref={buttonRef}
        title={title}
      >
        {!!icon && (
          <i aria-hidden="true" className={`codicon codicon-${icon}`} />
        )}
        {!iconOnly && children}
      </button>
    </span>
  );
}
