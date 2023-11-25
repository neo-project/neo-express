import React from "react";

import * as neonTypes from "@cityofzion/neon-core/lib/types";
import * as neonTx from "@cityofzion/neon-core/lib/tx";

import Address from "../Address";
import AutoCompleteData from "../../../shared/autoCompleteData";
import Hash from "../Hash";
import MetadataBadge from "../MetadataBadge";
import Script from "./Script";
import Table from "../Table";
import Time from "../Time";

type Props = {
  autoCompleteData: AutoCompleteData;
  block: Partial<neonTypes.BlockJson>;
  selectedTransactionHash?: string;
  selectAddress: (address: string) => void;
  selectTransaction: (txid: string) => void;
};

export default function BlockDetails({
  autoCompleteData,
  block,
  selectedTransactionHash,
  selectAddress,
  selectTransaction,
}: Props) {
  return (
    <div
      style={{
        display: "flex",
        flexWrap: "wrap",
        justifyContent: "center",
        alignItems: "stretch",
      }}
    >
      {!!block.time && (
        <MetadataBadge title="Time">
          <Time ts={block.time} />
        </MetadataBadge>
      )}
      {!!block.hash && (
        <MetadataBadge title="Block hash">
          <Hash hash={block.hash} />
        </MetadataBadge>
      )}
      <MetadataBadge title="Size">
        {block.size?.toLocaleString()} bytes
      </MetadataBadge>
      <MetadataBadge title="Version">{block.version}</MetadataBadge>
      {!!block.merkleroot && (
        <MetadataBadge title="Merkle root">
          <Hash hash={block.merkleroot} />
        </MetadataBadge>
      )}
      {!!block.nextconsensus && (
        <MetadataBadge title="Next consensus">
          <Hash hash={block.nextconsensus} />
        </MetadataBadge>
      )}
      {!!block.witnesses &&
        block.witnesses.map((witness) => (
          <MetadataBadge
            alignLeft
            grow
            key={`${witness.invocation}-${witness.verification}`}
            title="Witness"
          >
            <div>
              <strong>
                <small>Invocation</small>
              </strong>
              <br />
              <Script
                autoCompleteData={autoCompleteData}
                script={witness.invocation}
                selectAddress={selectAddress}
              />
            </div>
            <div style={{ marginTop: 4 }}>
              <strong>
                <small>Verification</small>
              </strong>
              <br />
              <Script
                autoCompleteData={autoCompleteData}
                script={witness.verification}
                selectAddress={selectAddress}
              />
            </div>
          </MetadataBadge>
        ))}
      {!!block.tx?.length && (
        <div style={{ width: "100%", marginTop: 10 }}>
          <Table
            headings={[
              { content: <>TXID</> },
              { content: <>Sender</> },
              { content: <>Size</> },
            ]}
            rows={block.tx.map((tx: Partial<neonTx.TransactionJson>) => ({
              onClick:
                selectedTransactionHash === tx.hash
                  ? () => selectTransaction("")
                  : () => selectTransaction(tx.hash || ""),
              key: tx.hash,
              cells: [
                { content: <Hash hash={tx.hash || ""} /> },
                {
                  content: !!tx.sender ? (
                    <Address
                      address={tx.sender}
                      addressNames={autoCompleteData.addressNames}
                    />
                  ) : (
                    <>Unknown sender</>
                  ),
                },
                { content: <>{(tx.size || 0).toLocaleString()} bytes</> },
              ],
              selected: selectedTransactionHash === tx.hash,
            }))}
          />
        </div>
      )}
    </div>
  );
}
