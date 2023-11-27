import React from "react";

import NavButton from "../NavButton";

type Props = {
  connectionName: string;
  onDeploy: () => void;
};

export default function DeployContract({ connectionName, onDeploy }: Props) {
  return (
    <>
      <div style={{ margin: 10, textAlign: "left" }}>
        There is a contract in your Visual Studio Code workspace that is not
        currently deployed to <strong>{connectionName}</strong>.
      </div>
      <NavButton style={{ margin: 10 }} onClick={onDeploy}>
        Deploy a contract
      </NavButton>
    </>
  );
}
