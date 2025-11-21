import React from "react";

import ConnectToBlockchain from "../quickStart/ConnectToBlockchain";
import CreateContract from "../quickStart/CreateContract";
import CreateNeoExpressInstance from "../quickStart/CreateNeoExpressInstance";
import CreateOrOpenWorkspace from "../quickStart/CreateOrOpenWorkspace";
import CreateWallet from "../quickStart/CreateWallet";
import DeployContract from "../quickStart/DeployContract";
import OpenBlockchainExplorer from "../quickStart/OpenBlockchainExplorer";
import QuickStartViewRequest from "../../../shared/messages/quickStartViewRequest";
import QuickStartViewState from "../../../shared/viewState/quickStartViewState";
import StartNeoExpress from "../quickStart/StartNeoExpress";
import InvokeContract from "../quickStart/InvokeContract";
import NavButton from "../NavButton";
import {
  getQuickStartActions,
  QuickStartAction,
} from "./quickStartActions";

type Props = {
  viewState: QuickStartViewState;
  postMessage: (message: QuickStartViewRequest) => void;
};

export default function QuickStart({ viewState, postMessage }: Props) {
  const actions: JSX.Element[] = [];
  const actionList = getQuickStartActions(viewState);

  actionList.forEach((actionKey) => {
    actions.push(renderAction(actionKey, viewState, postMessage));
  });

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        justifyContent: "space-evenly",
        alignItems: "center",
        textAlign: "center",
        minHeight: "calc(100% - 20px)",
        padding: 10,
      }}
    >
      {actions}
    </div>
  );
}

function renderAction(
  action: QuickStartAction,
  viewState: QuickStartViewState,
  postMessage: (message: QuickStartViewRequest) => void
): JSX.Element {
  switch (action) {
    case "createExpressInstance":
      return (
        <CreateNeoExpressInstance
          key="createNeoExpressInstance"
          onCreate={() =>
            postMessage({ command: "neo3-visual-devtracker.express.create" })
          }
        />
      );
    case "startExpress":
      return (
        <StartNeoExpress
          key="startNeoExpress"
          onStart={() =>
            postMessage({ command: "neo3-visual-devtracker.express.run" })
          }
        />
      );
    case "createExpressWallet":
      return (
        <NavButton
          key="createExpressWallet"
          style={{ margin: 10 }}
          onClick={() =>
            postMessage({
              command: "neo3-visual-devtracker.express.walletCreate",
            })
          }
        >
          Create a Neo Express wallet
        </NavButton>
      );
    case "createContract":
      return (
        <CreateContract
          key="createContract"
          onCreate={() =>
            postMessage({ command: "neo3-visual-devtracker.neo.newContract" })
          }
        />
      );
    case "deployExpressContract":
      return (
        <DeployContract
          key="deployContractNeo"
          connectionName={viewState.connectionName || ""}
          onDeploy={() =>
            postMessage({
              command: "neo3-visual-devtracker.express.contractDeploy",
            })
          }
        />
      );
    case "deployContract":
      return (
        <DeployContract
          key="deployContractNeoExpress"
          connectionName={viewState.connectionName || ""}
          onDeploy={() =>
            postMessage({
              command: "neo3-visual-devtracker.neo.contractDeploy",
            })
          }
        />
      );
    case "invokeContract":
      return (
        <InvokeContract
          key="invokeContract"
          onInvoke={() =>
            postMessage({
              command: "neo3-visual-devtracker.neo.invokeContract",
            })
          }
        />
      );
    case "connect":
      return (
        <ConnectToBlockchain
          key="connectToBlockchain"
          onConnect={() =>
            postMessage({ command: "neo3-visual-devtracker.connect" })
          }
        />
      );
    case "createWallet":
      return (
        <CreateWallet
          key="createWallet"
          onCreate={() =>
            postMessage({
              command: "neo3-visual-devtracker.neo.walletCreate",
            })
          }
        />
      );
    case "transfer":
      return (
        <NavButton
          key="transferAssets"
          style={{ margin: 10 }}
          onClick={() =>
            postMessage({
              command: "neo3-visual-devtracker.express.transfer",
            })
          }
        >
          Transfer assets between wallets
        </NavButton>
      );
    case "createCheckpoint":
      return (
        <NavButton
          key="createCheckpoint"
          style={{ margin: 10 }}
          onClick={() =>
            postMessage({
              command: "neo3-visual-devtracker.express.createCheckpoint",
            })
          }
        >
          Create a checkpoint
        </NavButton>
      );
    case "restoreCheckpoint":
      return (
        <NavButton
          key="restoreCheckpoint"
          style={{ margin: 10 }}
          onClick={() =>
            postMessage({
              command: "neo3-visual-devtracker.express.restoreCheckpoint",
            })
          }
        >
          Restore a checkpoint
        </NavButton>
      );
    case "createOrOpenWorkspace":
      return (
        <CreateOrOpenWorkspace
          key="createOrOpenWorkspace"
          onOpen={() => postMessage({ command: "vscode.openFolder" })}
        />
      );
    case "openExplorer":
      return (
        <OpenBlockchainExplorer
          key="openBlockchainExplorer"
          onOpen={() =>
            postMessage({ command: "neo3-visual-devtracker.tracker.openTracker" })
          }
        />
      );
    default:
      return <></>;
  }
}
