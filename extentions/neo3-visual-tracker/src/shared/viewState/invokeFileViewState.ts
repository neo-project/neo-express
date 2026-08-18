import AutoCompleteData from "../autoCompleteData";
import {
  InvocationAccount,
  WitnessScope,
} from "../invocationExecution";
import RecentTransaction from "../recentTransaction";

type InvokeFileViewState = {
  view: "invokeFile";
  panelTitle: string;
  autoCompleteData: AutoCompleteData;
  collapseTransactions: boolean;
  comments: string[];
  connectionHealthy: boolean;
  connectionName: string | null;
  executionAccounts: InvocationAccount[];
  errorText: string;
  fileContents: {
    contract?: string;
    operation?: string;
    args?: any[];
  }[];
  fileContentsJson: string;
  isExpressConnection: boolean;
  isPartOfDiffView: boolean;
  isReadOnly: boolean;
  jsonMode: boolean;
  recentTransactions: RecentTransaction[];
  selectedAccount: string | null;
  selectedTransactionId: string | null;
  witnessScope: WitnessScope;
};

export default InvokeFileViewState;
