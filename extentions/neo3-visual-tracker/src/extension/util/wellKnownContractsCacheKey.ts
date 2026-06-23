// Builds the globalState cache key used to invalidate the cached well-known
// contract manifests across neo-express versions. The version string is capped
// at 256 characters (substring(0, 256)) so an unexpectedly long version output
// cannot produce an unbounded key.
export default function wellKnownContractsCacheKey(versionOutput: string): string {
  return `wellKnownContracts_${versionOutput.trim().substring(0, 256)}`;
}
