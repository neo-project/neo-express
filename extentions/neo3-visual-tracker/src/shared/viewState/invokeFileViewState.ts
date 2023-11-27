import AutoCompleteData from "../autoCompleteData";
import RecentTransaction from "../recentTransaction";

type InvokeFileViewState = {
  view: "invokeFile";
  panelTitle: string;
  autoCompleteData: AutoCompleteData;
  collapseTransactions: boolean;
  comments: string[];
  errorText: string;
  fileContents: {
    contract?: string;
    operation?: string;
    args?: any[];
  }[];
  fileContentsJson: string;
  isPartOfDiffView: boolean;
  isReadOnly: boolean;
  jsonMode: boolean;
  recentTransactions: RecentTransaction[];
  selectedTransactionId: string | null;
};

export default InvokeFileViewState;
