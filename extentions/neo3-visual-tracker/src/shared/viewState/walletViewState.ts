import AddressInfo from "../addressInfo";
import AutoCompleteData from "../autoCompleteData";

type WalletViewState = {
  view: "wallet";
  panelTitle: string;
  autoCompleteData: AutoCompleteData;
  address: string;
  addressInfo: AddressInfo | null;
  offline: boolean;
};

export default WalletViewState;
