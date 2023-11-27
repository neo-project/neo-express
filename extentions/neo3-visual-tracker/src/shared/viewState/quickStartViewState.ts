type QuickStartViewState = {
  view: "quickStart";
  panelTitle: "";
  connectionName: string | null;
  hasContracts: boolean;
  hasDeployedContract: boolean;
  hasNeoExpressInstance: boolean;
  hasWallets: boolean;
  neoDeploymentRequired: boolean;
  neoExpressDeploymentRequired: boolean;
  neoExpressIsRunning: boolean;
  workspaceIsOpen: boolean;
};

export default QuickStartViewState;
