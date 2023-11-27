import React from "react";

import * as neonTypes from "@cityofzion/neon-core/lib/types";

import NavButton from "../NavButton";

type Props = {
  blocks: (neonTypes.BlockJson | null)[];
  blockHeight: number;
  paginationDistance: number;
  startAtBlock: number;
  style?: React.CSSProperties;
  setStartAtBlock: (newStartAtBlock: number) => void;
};

export default function BlockNavigation({
  blocks,
  blockHeight,
  paginationDistance,
  startAtBlock,
  style,
  setStartAtBlock,
}: Props) {
  if (!blocks.length) {
    return <></>;
  }
  const lastBlock =
    blocks[Math.min(paginationDistance, blocks.length - 1)]?.index ||
    Number.MAX_SAFE_INTEGER;
  const buttonStyle: React.CSSProperties = {
    margin: "0.25em",
  };
  return (
    <div style={style}>
      <span style={{ marginRight: "2em" }}>
        <NavButton
          style={buttonStyle}
          disabled={startAtBlock < 0 || startAtBlock >= blockHeight - 1}
          onClick={() => setStartAtBlock(-1)}
        >
          &lt;&lt; Most recent
        </NavButton>
        <NavButton
          style={buttonStyle}
          disabled={startAtBlock < 0 || startAtBlock >= blockHeight - 1}
          onClick={() => {
            const goto = startAtBlock + paginationDistance;
            setStartAtBlock(goto >= blockHeight ? -1 : goto);
          }}
        >
          &lt; Backwards
        </NavButton>
      </span>
      <span>
        <NavButton
          style={buttonStyle}
          disabled={lastBlock === 0}
          onClick={() =>
            setStartAtBlock(
              startAtBlock === -1
                ? blockHeight - paginationDistance
                : Math.max(
                    startAtBlock - paginationDistance,
                    paginationDistance
                  )
            )
          }
        >
          Forwards &gt;
        </NavButton>
        <NavButton
          style={buttonStyle}
          disabled={lastBlock === 0}
          onClick={() => setStartAtBlock(paginationDistance - 1)}
        >
          Oldest &gt;&gt;
        </NavButton>
      </span>
    </div>
  );
}
