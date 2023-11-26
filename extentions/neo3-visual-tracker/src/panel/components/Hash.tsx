import { reverseHex } from "@cityofzion/neon-core/lib/u";
import React from "react";

type Props = {
  hash: string;
  reverse?: boolean;
};

export default function Hash({ hash, reverse }: Props) {
  return (
    <span
      style={{
        fontFamily: "monospace",
        wordBreak: "break-all",
      }}
    >
      { (!reverse) ? hash : `0x${reverseHex(hash.substring(2))}`}
    </span>
  );
}
