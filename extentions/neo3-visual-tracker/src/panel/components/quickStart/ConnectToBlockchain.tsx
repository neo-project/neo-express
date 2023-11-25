import React from "react";

import NavButton from "../NavButton";

type Props = {
  onConnect: () => void;
};

export default function ConnectToBlockchain({ onConnect }: Props) {
  return (
    <>
      <div style={{ margin: 10, textAlign: "left" }}>
        When you connect to a blockchain you will be able to see autocomplete
        suggestions within VS Code based on contracts deployed to the
        blockchain.
      </div>
      <NavButton style={{ margin: 10 }} onClick={onConnect}>
        Connect to a blockchain
      </NavButton>
    </>
  );
}
