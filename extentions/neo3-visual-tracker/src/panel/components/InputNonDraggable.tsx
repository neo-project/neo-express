import React from "react";

type Props = {
  ariaLabel?: string;
  className?: string;
  disabled?: boolean;
  id?: string;
  list?: string;
  inputRef?: React.RefObject<HTMLInputElement>;
  style?: React.CSSProperties;
  type: string;
  value?: string;
  onBlur?: (e: React.FocusEvent<HTMLInputElement>) => void;
  onChange?: (e: React.ChangeEvent<HTMLInputElement>) => void;
  onFocus?: (e: React.FocusEvent<HTMLInputElement>) => void;
  onKeyDown?: (e: React.KeyboardEvent<HTMLInputElement>) => void;
};

export default function InputNonDraggable({
  ariaLabel,
  className,
  disabled,
  id,
  list,
  inputRef,
  style,
  type,
  value,
  onBlur,
  onChange,
  onFocus,
  onKeyDown,
}: Props) {
  return (
    <input
      aria-label={ariaLabel}
      className={className}
      disabled={disabled}
      draggable={true}
      id={id}
      list={list}
      ref={inputRef}
      style={style}
      type={type}
      value={value}
      onBlur={onBlur}
      onChange={onChange}
      onDragStart={(e) => {
        e.stopPropagation();
        e.preventDefault();
      }}
      onFocus={onFocus}
      onKeyDown={onKeyDown}
    />
  );
}
