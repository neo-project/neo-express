import React, { useEffect, useState } from "react";

import Contract from "./components/views/Contract";
import ContractViewState from "../shared/viewState/contractViewState";
import ControllerRequest from "../shared/messages/controllerRequest";
import InvokeFile from "./components/views/InvokeFile";
import InvokeFileViewState from "../shared/viewState/invokeFileViewState";
import LoadingIndicator from "./components/LoadingIndicator";
import QuickStart from "./components/views/QuickStart";
import QuickStartViewState from "../shared/viewState/quickStartViewState";
import StorageExplorer from "./components/views/StorageExplorer";
import StorageExplorerViewState from "../shared/viewState/storageExplorerViewState";
import Tracker from "./components/views/Tracker";
import TrackerViewState from "../shared/viewState/trackerViewState";
import View from "../shared/view";
import ViewRequest from "../shared/messages/viewRequest";
import ViewStateBase from "../shared/viewState/viewStateBase";
import Wallet from "./components/views/Wallet";
import WalletViewState from "../shared/viewState/walletViewState";

declare var acquireVsCodeApi: any;
const vscode = acquireVsCodeApi();

export default function ViewRouter() {
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [view, setView] = useState<View | null>(null);
  const [viewState, setViewState] = useState<ViewStateBase | null>(null);
  const postMessage = (request: ViewRequest) => {
    console.log("📤", request);
    vscode.postMessage(request);
  };
  const receiveMessage = (request: ControllerRequest) => {
    console.log("📬", request);
    if (request.viewState) {
      if (request.viewState.view !== view) {
        // Replace viewstate:
        setView(request.viewState.view);
        setViewState(request.viewState);
      } else {
        // Merge viewstate:
        setViewState((existing: any) => ({
          ...existing,
          ...request.viewState,
        }));
      }
    }
    if (request.loadingState) {
      setIsLoading(request.loadingState.isLoading);
    }
  };
  useEffect(() => {
    window.addEventListener("message", (msg) => receiveMessage(msg.data));
    postMessage({ retrieveViewState: true });
  }, []);
  let panelContent = <div></div>;
  if (!!view && !!viewState) {
    switch (view) {
      case "contract":
        panelContent = (
          <Contract
            viewState={viewState as ContractViewState}
            postMessage={(typedRequest) => postMessage({ typedRequest })}
          />
        );
        break;
      case "invokeFile":
        panelContent = (
          <InvokeFile
            viewState={viewState as InvokeFileViewState}
            postMessage={(typedRequest) => postMessage({ typedRequest })}
          />
        );
        break;
      case "quickStart":
        panelContent = (
          <QuickStart
            viewState={viewState as QuickStartViewState}
            postMessage={(typedRequest) => postMessage({ typedRequest })}
          />
        );
        break;
      case "storageExplorer":
        panelContent = (
          <StorageExplorer
            viewState={viewState as StorageExplorerViewState}
            postMessage={(typedRequest) => postMessage({ typedRequest })}
          />
        );
        break;
      case "tracker":
        panelContent = (
          <Tracker
            viewState={viewState as TrackerViewState}
            postMessage={(typedRequest) => postMessage({ typedRequest })}
          />
        );
        break;
      case "wallet":
        panelContent = (
          <Wallet
            viewState={viewState as WalletViewState}
            postMessage={(typedRequest) => postMessage({ typedRequest })}
          />
        );
        break;
    }
  }
  return (
    <>
      {panelContent}
      {(isLoading || !view || !viewState) && <LoadingIndicator />}
    </>
  );
}
