import * as neonTypes from "@cityofzion/neon-core/lib/types";
import * as neonTx from "@cityofzion/neon-core/lib/tx";

import AddressInfo from "../addressInfo";
import ApplicationLog from "../applicationLog";
import AutoCompleteData from "../autoCompleteData";

type TrackerViewState = {
  view: "tracker";
  panelTitle: string;
  autoCompleteData: AutoCompleteData;
  blockHeight: number;
  blocks: (neonTypes.BlockJson | null)[];
  paginationDistance: number;
  populatedBlocksFilterEnabled: boolean;
  populatedBlocksFilterSupported: boolean;
  searchHistory: string[];
  selectedAddress: AddressInfo | null;
  selectedBlock: neonTypes.BlockJson | null;
  selectedTransaction: {
    tx: neonTx.TransactionJson;
    log?: ApplicationLog;
  } | null;
  startAtBlock: number;
};

export default TrackerViewState;
