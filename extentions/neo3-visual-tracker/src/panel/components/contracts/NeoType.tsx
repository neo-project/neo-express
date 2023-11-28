import React from "react";

type Props = {
  type?: string | number;
};

const LOOKUP_TABLE: any = {
  0: "Any",
  Any: "Any",
  16: "Boolean",
  Boolean: "Boolean",
  17: "Integer",
  Integer: "Integer",
  18: "ByteArray",
  ByteArray: "ByteArray",
  19: "String",
  String: "String",
  20: "Hash160",
  Hash160: "Hash160",
  21: "Hash256",
  Hash256: "Hash256",
  22: "PublicKey",
  PublicKey: "PublicKey",
  23: "Signature",
  Signature: "Signature",
  32: "Array",
  Array: "Array",
  34: "Map",
  Map: "Map",
  48: "InteropInterface",
  InteropInterface: "InteropInterface",
  255: "Void",
  Void: "Void",
};

export default function NeoType({ type }: Props) {
  return !!type ? (
    <>{LOOKUP_TABLE[type] || "Unknown type"}</>
  ) : (
    <>Unknown type</>
  );
}
