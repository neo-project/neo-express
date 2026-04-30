type ServerListConfig = {
  blockchainNames: { [genesisHash: string]: string };
  rpcUrls: string[];
};

function isRecord(value: unknown): value is { [key: string]: unknown } {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function normalizeString(value: unknown): string | undefined {
  return typeof value === "string" ? value.trim() : undefined;
}

export default function parseServerListConfig(contents: unknown): ServerListConfig {
  const blockchainNames: { [genesisHash: string]: string } = {};
  const rpcUrls: string[] = [];

  if (!isRecord(contents)) {
    return { blockchainNames, rpcUrls };
  }

  const configuredNames = contents["neo-blockchain-names"];
  if (isRecord(configuredNames)) {
    for (const [genesisHash, rawName] of Object.entries(configuredNames)) {
      const normalizedHash = genesisHash.toLowerCase().trim();
      const name = normalizeString(rawName);
      if (normalizedHash && name) {
        blockchainNames[normalizedHash] = name;
      }
    }
  }

  const configuredRpcUrls = contents["neo-rpc-uris"];
  if (Array.isArray(configuredRpcUrls)) {
    for (const rawUrl of configuredRpcUrls) {
      const url = normalizeString(rawUrl);
      if (url) {
        rpcUrls.push(url);
      }
    }
  }

  return { blockchainNames, rpcUrls };
}
