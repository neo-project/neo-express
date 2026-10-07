import AutoCompleteData from "./autoCompleteData";

type ManifestLike = {
  abi?: {
    methods?: Array<{
      name: string;
      parameters?: Array<{ name: string; type: string }>;
      returntype?: string;
      safe?: boolean;
    }>;
  };
};

export function stripContractPrefix(contract?: string): string {
  if (!contract) {
    return "";
  }
  return contract.startsWith("#") ? contract.slice(1) : contract;
}

export function resolveContractManifest(
  autoCompleteData: AutoCompleteData,
  contract?: string
): ManifestLike | undefined {
  const name = stripContractPrefix(contract);
  if (!name) {
    return undefined;
  }

  const direct = autoCompleteData.contractManifests[name];
  if (direct) {
    return direct as ManifestLike;
  }

  const lower = name.toLowerCase();
  for (const [key, manifest] of Object.entries(
    autoCompleteData.contractManifests
  )) {
    if (key.toLowerCase() === lower) {
      return manifest as ManifestLike;
    }
    const mappedName = autoCompleteData.contractNames[key];
    if (mappedName && mappedName.toLowerCase() === lower) {
      return manifest as ManifestLike;
    }
  }

  for (const [key, paths] of Object.entries(autoCompleteData.contractPaths)) {
    if (
      key.toLowerCase() === lower ||
      paths.some((p) => p.toLowerCase().includes(`/${lower}.`) || p.toLowerCase().includes(`\\${lower}.`))
    ) {
      return autoCompleteData.contractManifests[key] as ManifestLike;
    }
  }

  return undefined;
}

export function manifestMethods(manifest?: ManifestLike) {
  const methods = manifest?.abi?.methods ?? [];
  return methods.filter((method) => method?.name && !method.name.startsWith("_"));
}
