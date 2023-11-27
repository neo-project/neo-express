import * as neonSc from "@cityofzion/neon-core/lib/sc";

import AddressNames from "./addressNames";
import ContractNames from "./contractNames";

type AutoCompleteData = {
  contractManifests: {
    [contractHashOrName: string]: Partial<neonSc.ContractManifestJson>;
  };
  contractNames: ContractNames;
  contractPaths: { [contractHashOrName: string]: string[] };
  wellKnownAddresses: { [addressName: string]: string };
  addressNames: AddressNames;
};

export default AutoCompleteData;
