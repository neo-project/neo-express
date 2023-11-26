import React from "react";
import * as neonCore from "@cityofzion/neon-core";

import AddressNames from "../../shared/addressNames";
import NavLink from "./NavLink";

type Props = {
  address: string;
  addressNames: AddressNames;
  onClick?: (address: string) => void;
};

export default function Address({ address, addressNames, onClick }: Props) {
  const style: React.CSSProperties = {
    fontFamily: "monospace",
    wordBreak: "break-all",
  };
  if (address.startsWith("0x")) {
    try {
      address = neonCore.wallet.getAddressFromScriptHash(address.substring(2));
    } catch {
      return <span style={style}>{address}</span>;
    }
  }
  const names = addressNames[address];
  const primaryName = names?.length
    ? `${address.substring(0, 4)}..${address.substring(address.length - 4)} (${
        names[0]
      })`
    : address;
  const title = names?.length
    ? `Address:\n ${address}\n  (${names.join(", ")})`
    : `Address:\n ${address}`;
  return !!onClick ? (
    <NavLink
      style={style}
      title={title}
      onClick={(e) => {
        e.stopPropagation();
        onClick(address);
      }}
    >
      {primaryName}
    </NavLink>
  ) : (
    <span style={style} title={title}>
      {primaryName}
    </span>
  );
}
