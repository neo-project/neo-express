import React from "react";

import * as neonTypes from "@cityofzion/neon-core/lib/types";

import Hash from "../Hash";
import Table from "../Table";
import Time from "../Time";

type Props = {
  blocks: (neonTypes.BlockJson | null)[];
  populatedBlocksFilterEnabled: boolean;
  populatedBlocksFilterSupported: boolean;
  selectedBlock: neonTypes.BlockJson | null;
  selectBlock: (hash: string) => void;
  togglePopulatedBlocksFilter: (enabled: boolean) => void;
};

export default function BlockList({
  blocks,
  populatedBlocksFilterEnabled,
  populatedBlocksFilterSupported,
  selectedBlock,
  selectBlock,
  togglePopulatedBlocksFilter,
}: Props) {
  const loadingStyle: React.CSSProperties = {
    textAlign: "center",
    padding: 30,
  };
  const startingParity = blocks[0]?.index || 0;
  return (
    <>
      {populatedBlocksFilterSupported && (
        <div
          style={{
            fontSize: "1.05rem",
            marginBottom: 5,
            marginLeft: 10,
            marginTop: 5,
          }}
        >
          <label>
            <input
              type="checkbox"
              checked={populatedBlocksFilterEnabled}
              onChange={(e) => togglePopulatedBlocksFilter(e.target.checked)}
              style={{ marginRight: 8 }}
            />
            Hide empty blocks
          </label>
        </div>
      )}
      <Table
        headings={[
          { content: <>Index</> },
          { content: <>Time</> },
          { content: <>Transactions</> },
          { content: <>Hash</> },
          { content: <>Size</> },
        ]}
        rows={
          blocks.length
            ? blocks.map((block, i) => ({
                key: block?.hash || `missing_${i}`,
                parity: (startingParity + i) % 2 === 0,
                onClick: block ? () => selectBlock(block.hash) : undefined,
                cells: [
                  {
                    content: (
                      <>
                        {block?.index === undefined
                          ? "..."
                          : block.index.toLocaleString()}
                      </>
                    ),
                  },
                  { content: block ? <Time ts={block.time} /> : <>...</> },
                  { content: <>{(block?.tx?.length || 0).toLocaleString()}</> },
                  { content: <Hash hash={block?.hash || "..."} /> },
                  { content: <>{(block?.size || 0).toLocaleString()} bytes</> },
                ],
                selected: selectedBlock?.hash === block?.hash,
              }))
            : [
                {
                  key: "loading",
                  cells: [
                    {
                      colSpan: 5,
                      content: (
                        <span style={loadingStyle}>Loading&hellip;</span>
                      ),
                    },
                  ],
                },
              ]
        }
      />
    </>
  );
}
