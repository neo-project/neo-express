import React, { useState } from "react";
import * as buffer from "buffer";
import * as neonCore from "@cityofzion/neon-core";

import Address from "../Address";
import AutoCompleteData from "../../../shared/autoCompleteData";
import reverseBytes from "./reverseBytes";

type Props = {
  autoCompleteData: AutoCompleteData;
  token: string;
  selectAddress?: (address: string) => void;
};

export default function ScriptToken({
  autoCompleteData,
  token,
  selectAddress,
}: Props) {
  const [isExpanded, setIsExpanded] = useState(false);
  token = token.trim();

  const getAbbreviatedElement = () => {
    const contractHashes = Object.keys(autoCompleteData.contractNames);
    for (const contractHash of contractHashes) {
      const name = autoCompleteData.contractNames[contractHash] || "contract";
      const contractHashRaw = contractHash.replace(/^0x/g, "").toLowerCase();
      if (reverseBytes(token) === contractHashRaw) {
        return [
          <span title={`Contract:\n ${contractHashRaw}\n  (${name})`}>
            <strong>
              {contractHashRaw.substring(0, 4)}..
              {contractHashRaw.substring(contractHashRaw.length - 4)} (
            </strong>
            <i>{name}</i>
            <strong>)</strong>
          </span>,
          reverseBytes(token),
        ];
      }
    }

    if (token.length == 40) {
      try {
        const address = neonCore.wallet.getAddressFromScriptHash(
          reverseBytes(token)
        );
        if (address.startsWith("N")) {
          return [
            <>
              <strong title={token}>
                {token.substring(0, 4)}..
                {token.substring(token.length - 4)}
              </strong>{" "}
              <Address
                address={address}
                addressNames={autoCompleteData.addressNames}
                onClick={selectAddress}
              />
            </>,
            token,
          ];
        }
      } catch {}
    }

    try {
      const asText = buffer.Buffer.from(token, "hex")
        .toString("ascii")
        .replace(/\\n/g, " ")
        .trim();
      // Strings less than two characters in length are probably more useful expressed as numbers
      if (asText.length > 2) {
        let printableAscii = true;
        for (let i = 0; i < asText.length; i++) {
          const c = asText.charCodeAt(i);
          printableAscii = printableAscii && c >= 32 && c <= 126;
        }
        if (printableAscii) {
          return [
            <span title={`Detected text:\n0x${token} =\n"${asText}"`}>
              <strong>
                {" "}
                {token.length > 8 ? (
                  <>
                    {token.substring(0, 4)}..
                    {token.substring(token.length - 4)}
                  </>
                ) : (
                  <>{token}</>
                )}{" "}
                ("
              </strong>
              <i>{asText}</i>
              <strong>")</strong>
            </span>,
            token,
          ];
        }
      }
    } catch {}

    try {
      const numericalValue = parseInt(reverseBytes(token), 16);
      if (
        !!token.match(/^([a-f0-9][a-f0-9])+$/i) &&
        !isNaN(numericalValue) &&
        numericalValue < Math.pow(2, 64)
      ) {
        return [
          <span title={`0x${token} = ${numericalValue}`}>
            <strong>
              {" "}
              {token.length > 8 ? (
                <>
                  {token.substring(0, 4)}..
                  {token.substring(token.length - 4)}
                </>
              ) : (
                <>{token}</>
              )}{" "}
              (
            </strong>
            <i>{numericalValue}</i>
            <strong>)</strong>
          </span>,
          token,
        ];
      }
    } catch {}

    return [null, token];
  };

  let [innerElement, unabbreviated] = getAbbreviatedElement();
  const isAbbreviated = innerElement !== null;
  innerElement = innerElement || <>{token}</>;
  return (
    <span
      style={{
        marginRight: "1em",
        wordBreak: "break-all",
        cursor: isAbbreviated ? "pointer" : undefined,
      }}
      onClick={(e) => {
        setIsExpanded(!isExpanded);
        if (!isExpanded) {
          const selection = window.getSelection();
          if (selection) {
            selection.removeAllRanges();
            const range = document.createRange();
            range.selectNode(e.currentTarget);
            selection.addRange(range);
          }
        }
      }}
    >
      {isExpanded ? unabbreviated : innerElement}
    </span>
  );
}
