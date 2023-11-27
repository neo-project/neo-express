import React from "react";

import AddressInfo from "../../../shared/addressInfo";
import AutoCompleteData from "../../../shared/autoCompleteData";
import ScriptToken from "./ScriptToken";

type Props = {
  addressInfo: AddressInfo;
  autoCompleteData: AutoCompleteData;
};

export default function AddressDetails({
  addressInfo,
  autoCompleteData,
}: Props) {
  const names = autoCompleteData.addressNames[addressInfo.address];
  return (
    <div style={{ textAlign: "center" }}>
      <p style={{ fontWeight: "bold", fontSize: "1.5rem" }}>
        {addressInfo.address}
        {!!names && !!names.length && (
          <>
            <br />
            <span style={{ fontSize: "1.25rem", fontWeight: "normal" }}>
              ({names.map((_) => `"${_}"`).join(", ")})
            </span>
          </>
        )}
      </p>
      <p style={{ marginBottom: 5 }}>
        <small>NEO balance:</small>
      </p>
      <p style={{ fontWeight: "bold", fontSize: "1.25rem", marginTop: 0 }}>
        {addressInfo.neoBalance.toLocaleString()} NEO
      </p>
      <p style={{ marginBottom: 5 }}>
        <small>GAS balance:</small>
      </p>
      <p style={{ fontWeight: "bold", fontSize: "1.25rem", marginTop: 0 }}>
        {(addressInfo.gasBalance / 100000000.0).toLocaleString()} GAS
      </p>
      {!!Object.keys(addressInfo.allBalances).length && (
        <>
          <p style={{ marginBottom: 5 }}>
            <small>All NEP 17 balances:</small>
          </p>
          <p style={{ marginTop: 0 }}>
            <table style={{ width: "100%" }}>
              <tbody>
                {Object.keys(addressInfo.allBalances).map((assetHash) => {
                  const balance = addressInfo.allBalances[assetHash];
                  return (
                    <tr key={assetHash}>
                      <td>
                        <small>
                          <ScriptToken
                            token={assetHash.substring(2)}
                            autoCompleteData={autoCompleteData}
                          />
                        </small>
                      </td>
                      <th>
                        <small>{balance.toLocaleString()}</small>
                      </th>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </p>
        </>
      )}
    </div>
  );
}
