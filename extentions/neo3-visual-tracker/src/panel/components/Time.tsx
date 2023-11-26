import React from "react";

type Props = {
  ts: number;
};

export default function Time({ ts }: Props) {
  const date = new Date(ts);
  return (
    <>
      {date.toLocaleDateString()}, {date.toLocaleTimeString()}
    </>
  );
}
