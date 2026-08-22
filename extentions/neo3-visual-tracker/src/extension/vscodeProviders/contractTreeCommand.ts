type ContractTreeData = {
  description?: string;
  hash?: string;
  name: string;
  path?: string;
};

export class ContractTreeItemData implements ContractTreeData {
  constructor(
    public readonly name: string,
    public readonly description?: string,
    public readonly hash?: string,
    public readonly path?: string
  ) {}
}

export function getWorkspaceContractPath(context: unknown) {
  return context instanceof ContractTreeItemData ? context.path : undefined;
}

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
