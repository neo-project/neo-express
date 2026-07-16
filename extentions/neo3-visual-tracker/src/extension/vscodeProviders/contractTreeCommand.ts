type ContractTreeData = {
  hash?: string;
  path?: string;
};

export default function getContractTreeCommand(contract: ContractTreeData) {
  if (!contract.hash) {
    return undefined;
  }

  return {
    command: "neo3-visual-devtracker.tracker.openContract",
    arguments: [{ hash: contract.hash }],
    title: contract.hash,
  };
}
