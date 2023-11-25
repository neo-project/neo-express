import AutoCompleteData from "../autoCompleteData";

type ContractViewState = {
  view: "contract";
  panelTitle: string;
  autoCompleteData: AutoCompleteData;
  contractHash: string;
};

export default ContractViewState;
