import React, { useEffect, useRef } from "react";

import NavButton from "./NavButton";

type Props = {
  affinity?: "top-left" | "middle" | "bottom-right";
  children: any;
  closeButtonText?: string;
  title?: string;
  onClose: () => void;
};

export default function Dialog({
  affinity,
  children,
  closeButtonText,
  title,
  onClose,
}: Props) {
  affinity = affinity || "middle";
  closeButtonText = closeButtonText || "Close";
  const titleId = useRef(
    `neo-dialog-title-${Math.random().toString(36).slice(2)}`
  ).current;
  const dialogRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    const previousActiveElement = document.activeElement as HTMLElement | null;
    const focusableSelector =
      'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])';
    const focusableElements = () =>
      Array.from(
        dialog?.querySelectorAll<HTMLElement>(focusableSelector) || []
      ).filter((element) => !element.hasAttribute("disabled"));

    focusableElements()[0]?.focus();
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        onClose();
        return;
      }
      if (event.key !== "Tab") {
        return;
      }
      const elements = focusableElements();
      if (!elements.length) {
        event.preventDefault();
        return;
      }
      const first = elements[0];
      const last = elements[elements.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      previousActiveElement?.focus();
    };
  }, [onClose]);

  return (
    <div
      className={`neo-dialog-backdrop neo-dialog-backdrop--${affinity}`}
      onClick={onClose}
    >
      <div
        aria-labelledby={title ? titleId : undefined}
        aria-modal="true"
        className="neo-dialog"
        onClick={(e) => e.stopPropagation()}
        ref={dialogRef}
        role="dialog"
      >
        <div className="neo-dialog__header">
          {!!title && (
            <h2 className="neo-dialog__title" id={titleId}>
              {title}
            </h2>
          )}
          <NavButton
            ariaLabel="Close dialog"
            icon="close"
            iconOnly
            onClick={onClose}
            title="Close"
            variant="ghost"
          />
        </div>
        <div className="neo-dialog__body">{children}</div>
        <div className="neo-dialog__footer">
          <NavButton clickOnEnter onClick={onClose} variant="secondary">
            {closeButtonText}
          </NavButton>
        </div>
      </div>
    </div>
  );
}
