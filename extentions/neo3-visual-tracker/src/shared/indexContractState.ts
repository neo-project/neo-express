import * as neonSc from "@cityofzion/neon-core/lib/sc";

import AutoCompleteData from "./autoCompleteData";

type ContractStateLike = {
  hash?: unknown;
  manifest?: Partial<neonSc.ContractManifestJson>;
};

type ContractIndex = Pick<
  AutoCompleteData,
  "contractManifests" | "contractNames"
>;

export default function indexContractState(
  index: ContractIndex,
  contractState: ContractStateLike,
  fallbackName = ""
) {
  const contractHash =
    typeof contractState.hash === "string" ? contractState.hash : "";
  const manifest = contractState.manifest;
  if (!contractHash || !manifest) {
    return false;
  }

  index.contractManifests[contractHash] = manifest;
  const manifestName =
    typeof manifest.name === "string" ? manifest.name.trim() : "";
  const contractName = manifestName || fallbackName.trim();
  if (contractName) {
    index.contractManifests[contractName] = manifest;
    index.contractNames[contractHash] = contractName;
  }

  return true;
}
