import React from "react";

import NavButton from "../NavButton";

type Props = {
  onOpen: () => void;
};

export default function CreateOrOpenWorkspace({ onOpen }: Props) {
  return (
    <>
      <div style={{ margin: 10, textAlign: "left" }}>
        We recommend that you create a new folder for your project and then open
        that folder in Visual Studio Code.
      </div>
      <NavButton style={{ margin: 10 }} onClick={onOpen}>
        Open or create a workspace folder
      </NavButton>
    </>
  );
}
