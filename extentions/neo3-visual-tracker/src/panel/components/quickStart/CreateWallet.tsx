import React from "react";

import NavButton from "../NavButton";

type Props = {
  onCreate: () => void;
};

export default function CreateWallet({ onCreate }: Props) {
  return (
    <>
      <div style={{ margin: 10, textAlign: "left" }}>
        In order to deploy your contracts to TestNet or MainNet you will need a
        NEP-6 wallet. For production use cases it is important that you protect
        the accounts in your wallet with a strong passphrase.
      </div>
      <NavButton style={{ margin: 10 }} onClick={onCreate}>
        Create a NEP-6 wallet
      </NavButton>
    </>
  );
}
