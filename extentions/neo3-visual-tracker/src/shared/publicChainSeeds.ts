export const MAINNET_GENESIS =
  "0x1f4d1defa46faa5e7b9b8d3f79a06bec777d7c26c4aa5f6f5899a291daa87c15";
export const TESTNET_GENESIS =
  "0x9d3276785e7306daf59a3f3b9e31912c095598bbfb8a4476b821b0e59be4c57a";

export function knownGenesisHashForSeed(rpcUrl: string): string | undefined {
  const lower = rpcUrl.toLowerCase();
  if (lower.includes("neo.org:10332")) {
    return MAINNET_GENESIS;
  }
  if (lower.includes("neo.org:20332")) {
    return TESTNET_GENESIS;
  }
  return undefined;
}
