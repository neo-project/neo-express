import posixPath from "./posixPath";

export function invokeFileNameForContract(contractName: string): string {
  return (
    (contractName || "Contract").replace(/[^-_.a-z0-9]/gi, "-") || "Contract"
  );
}

export function resolveInvokeFilePath(
  invokeFilesFolder: string,
  contractName: string,
  exists: (path: string) => boolean
): { path: string; reuse: boolean } {
  const safeContractName = invokeFileNameForContract(contractName);
  const path = posixPath(
    invokeFilesFolder,
    `${safeContractName}.neo-invoke.json`
  );
  return { path, reuse: exists(path) };
}
