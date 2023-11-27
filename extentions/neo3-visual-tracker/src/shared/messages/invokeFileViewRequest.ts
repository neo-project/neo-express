type InvokeFileViewRequest = {
  addStep?: boolean;
  close?: boolean;
  debugStep?: { i: number };
  deleteStep?: { i: number };
  moveStep?: { from: number; to: number };
  runAll?: boolean;
  runStep?: { i: number };
  selectTransaction?: { txid: string | null };
  toggleTransactions?: boolean;
  toggleJsonMode?: boolean;
  update?: {
    i: number;
    contract?: string;
    operation?: string;
    args?: any[];
  };
  updateJson?: string;
};

export default InvokeFileViewRequest;
