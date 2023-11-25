import React from "react";

type Props = {
  disabled?: boolean;
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
  disabled,
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
      disabled={disabled}
      draggable={true}
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
