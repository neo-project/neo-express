import React from "react";

import NavButton from "../NavButton";

type Props = {
  onCreate: () => void;
};

export default function CreateContract({ onCreate }: Props) {
  return (
    <>
      <div style={{ textAlign: "left" }}>
        Scaffold a C# (Blank, NEP-17, NEP-11, Oracle, Ownable), Python, or
        Java contract under <code>contracts/</code>.
      </div>
      <NavButton onClick={onCreate}>New contract</NavButton>
    </>
  );
}
