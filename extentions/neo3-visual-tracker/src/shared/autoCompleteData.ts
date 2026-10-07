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
  /** Display name -> value passed to neoxp (Express wallet name or supported NEP-6 path). */
  accountSigners?: { [addressName: string]: string };
};

export default AutoCompleteData;
