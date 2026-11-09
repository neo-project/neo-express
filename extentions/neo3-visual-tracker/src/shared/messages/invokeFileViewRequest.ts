type InvokeFileViewRequest = {
  addStep?: boolean;
  close?: boolean;
  connect?: boolean;
  debugStep?: { i: number };
  deleteStep?: { i: number };
  moveStep?: { from: number; to: number };
  runAll?: boolean;
  runStep?: { i: number };
  selectAccount?: { name: string };
  selectTransaction?: { txid: string | null };
  toggleTransactions?: boolean;
  toggleJsonMode?: boolean;
  updateWitnessScope?: { scope: string };
  update?: {
    i: number;
    contract?: string;
    operation?: string;
    args?: any[];
  };
  updateJson?: string;
};

export default InvokeFileViewRequest;
