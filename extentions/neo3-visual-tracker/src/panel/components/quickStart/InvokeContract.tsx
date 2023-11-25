import React from "react";

import NavButton from "../NavButton";

type Props = {
  onInvoke: () => void;
};

export default function InvokeContract({ onInvoke }: Props) {
  return (
    <>
      <div style={{ margin: 10, textAlign: "left" }}>
        Congratualtions on deploying your contract! Would you like to invoke it?
      </div>
      <NavButton style={{ margin: 10 }} onClick={onInvoke}>
        Invoke a contract
      </NavButton>
    </>
  );
}
