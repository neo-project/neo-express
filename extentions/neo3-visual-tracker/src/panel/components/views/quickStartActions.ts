import QuickStartViewState from "../../../shared/viewState/quickStartViewState";

export type QuickStartAction =
  | "createOrOpenWorkspace"
  | "openExplorer"
  | "createExpressInstance"
  | "startExpress"
  | "createExpressWallet"
  | "createContract"
  | "deployExpressContract"
  | "deployContract"
  | "invokeContract"
  | "connect"
  | "createWallet"
  | "transfer"
  | "createCheckpoint"
  | "restoreCheckpoint";

export function getQuickStartActions(
  viewState: QuickStartViewState
): QuickStartAction[] {
  const actions: QuickStartAction[] = [];

  if (viewState.workspaceIsOpen) {
    if (viewState.hasNeoExpressInstance) {
      if (!viewState.neoExpressIsRunning) {
        actions.push("startExpress");
      }
      actions.push("createExpressWallet");
    } else {
      actions.push("createExpressInstance");
    }

    if (!viewState.hasContracts) {
      actions.push("createContract");
    }

    if (viewState.connectionName) {
      if (viewState.neoExpressDeploymentRequired) {
        actions.push("deployExpressContract");
      } else if (viewState.neoDeploymentRequired) {
        actions.push("deployContract");
      } else if (viewState.hasDeployedContract) {
        actions.push("invokeContract");
      }
    } else {
      actions.push("connect");
    }

    if (!viewState.hasWallets) {
      actions.push("createWallet");
    } else if (viewState.hasNeoExpressInstance) {
      actions.push("transfer");
    }

    if (viewState.hasNeoExpressInstance) {
      actions.push("createCheckpoint");
      if (viewState.hasCheckpoints) {
        actions.push("restoreCheckpoint");
      }
    }
  } else {
    actions.push("createOrOpenWorkspace");
  }

  actions.push("openExplorer");
  return actions;
}
