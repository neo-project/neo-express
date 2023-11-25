import React from "react";

import NavButton from "../NavButton";

type Props = {
  onCreate: () => void;
};

export default function CreateNeoExpressInstance({ onCreate }: Props) {
  return (
    <>
      <div style={{ margin: 10, textAlign: "left" }}>
        Neo Express can be used to create private blockchains that you can use
        to test and debug your smart contracts locally.
      </div>
      <NavButton style={{ margin: 10 }} onClick={onCreate}>
        Create a new Neo Express Instance
      </NavButton>
    </>
  );
}
