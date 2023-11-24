import React, { Fragment } from "react";
import * as neonCore from "@cityofzion/neon-core";

import AutoCompleteData from "../../../shared/autoCompleteData";
import Address from "../Address";
import ApplicationLog from "../../../shared/applicationLog";
import Hash from "../Hash";
import MetadataBadge from "../MetadataBadge";
import Script from "./Script";
import ScriptToken from "./ScriptToken";
import TypedValueDisplay from "./TypedValueDisplay";

type Props = {
  autoCompleteData: AutoCompleteData;
  applicationLog?: ApplicationLog;
  transaction: Partial<neonCore.tx.TransactionJson>;
  selectAddress?: (address: string) => void;
};

export default function TransactionDetails({
  autoCompleteData,
  applicationLog,
  transaction,
  selectAddress,
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
      {!!transaction.hash && (
        <MetadataBadge title="TXID">
          <Hash hash={transaction.hash} />
        </MetadataBadge>
      )}
      {!!transaction.sender && (
        <MetadataBadge title="Sender">
          <Address
            address={transaction.sender}
            addressNames={autoCompleteData.addressNames}
            onClick={selectAddress}
          />
        </MetadataBadge>
      )}
      {!!transaction.signers?.length &&
        transaction.signers.map((signer, i) => (
          <MetadataBadge title="Signer" key={i}>
            <Address
              address={signer.account}
              addressNames={autoCompleteData.addressNames}
              onClick={selectAddress}
            />{" "}
            &mdash; {signer.scopes}
          </MetadataBadge>
        ))}
      {!!transaction.size && (
        <MetadataBadge title="Size">
          {transaction.size.toLocaleString()} bytes
        </MetadataBadge>
      )}
      {!!transaction.netfee && (
        <MetadataBadge title="Network fee">{transaction.netfee}</MetadataBadge>
      )}
      {!!transaction.sysfee && (
        <MetadataBadge title="System fee">{transaction.sysfee}</MetadataBadge>
      )}
      {!!transaction.nonce && (
        <MetadataBadge title="Nonce">{transaction.nonce}</MetadataBadge>
      )}
      {!!transaction.validuntilblock && (
        <MetadataBadge title="Valid until">
          {transaction.validuntilblock}
        </MetadataBadge>
      )}
      {!!transaction.version && (
        <MetadataBadge title="Version">{transaction.version}</MetadataBadge>
      )}
      {!!transaction.script && (
        <div style={{ width: "100%" }}>
          <MetadataBadge alignLeft grow title="Script">
            <Script
              autoCompleteData={autoCompleteData}
              script={transaction.script}
              selectAddress={selectAddress}
            />
          </MetadataBadge>
        </div>
      )}
      {(applicationLog?.executions || []).map((execution, i) => (
        <Fragment key={i}>
          <div
            style={{
              display: "flex",
              flexWrap: "wrap",
              justifyContent: "center",
              alignItems: "stretch",
            }}
          >
            <MetadataBadge title="Execution">#{i + 1}</MetadataBadge>
            <MetadataBadge title="Trigger">{execution.trigger}</MetadataBadge>
            <MetadataBadge title="VM State">{execution.vmstate}</MetadataBadge>
            <MetadataBadge title="Exception">
              {execution.exception || "(none)"}
            </MetadataBadge>
            <MetadataBadge title="Gas">{execution.gasconsumed}</MetadataBadge>
            {execution.stack?.map((stack, j) => (
              <MetadataBadge grow key={j} title={`Result ${j + 1}`} alignLeft>
                <TypedValueDisplay
                  autoCompleteData={autoCompleteData}
                  value={stack}
                  selectAddress={selectAddress}
                />
              </MetadataBadge>
            ))}
          </div>
          <div style={{ width: "100%" }}>
            {execution.notifications?.map((notification, j) => (
              <MetadataBadge
                key={j}
                alignLeft
                grow
                title={`Notification #${j + 1}`}
              >
                {!!notification.contract && (
                  <>
                    <ScriptToken
                      autoCompleteData={autoCompleteData}
                      token={notification.contract.substring(2)}
                      selectAddress={selectAddress}
                    />
                    fired: <strong>{notification.eventname}</strong> with state:
                  </>
                )}
                {!!notification.state && (
                  <TypedValueDisplay
                    autoCompleteData={autoCompleteData}
                    value={notification.state}
                    selectAddress={selectAddress}
                  />
                )}
              </MetadataBadge>
            ))}
          </div>
        </Fragment>
      ))}
      {!!transaction.witnesses?.length &&
        transaction.witnesses.map((witness, i) => (
          <div style={{ width: "100%" }} key={i}>
            <MetadataBadge alignLeft grow title="Witness">
              <div>
                <strong>
                  <small>Invocation</small>
                </strong>
                <br />
                <Script
                  autoCompleteData={autoCompleteData}
                  script={witness.invocation}
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
                />
              </div>
            </MetadataBadge>
          </div>
        ))}
    </div>
  );
}
