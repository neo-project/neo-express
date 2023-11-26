import React from "react";

import NavButton from "../NavButton";

type Props = {
  onCreate: () => void;
};

export default function CreateContract({ onCreate }: Props) {
  return (
    <>
      <div style={{ margin: 10, textAlign: "left" }}>
        You don't appear to have any smart contracts in the current Visual
        Studio Code worskspace. Would you like to create a new smart contract?
        You can write your smart contract's code in a programming language of
        your choice and then deploy and test it locally using Neo Express.
      </div>
      <NavButton style={{ margin: 10 }} onClick={onCreate}>
        Create a new contract
      </NavButton>
    </>
  );
}
