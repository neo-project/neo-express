export type CsharpStarter = {
  id: string;
  label: string;
  /** Overlay files from csharp-starters/<id> after the C# scaffold. */
  overlay: boolean;
};

/**
 * C# starters generated from Neo.SmartContract.Template.
 * The commit is pinned in samples/examples/devpack-source.json.
 * Official unit-test projects are not included.
 */
export const csharpStarters: CsharpStarter[] = [
  { id: "blank", label: "Blank contract", overlay: true },
  { id: "nep17", label: "NEP-17 token", overlay: true },
  { id: "nep11", label: "NEP-11 NFT", overlay: true },
  { id: "oracle", label: "Oracle", overlay: true },
  { id: "ownable", label: "Ownable", overlay: true },
  { id: "storage", label: "Storage (number map)", overlay: false },
];

export function csharpStarterLabels(): string[] {
  return csharpStarters.map((starter) => starter.label);
}

export function findCsharpStarter(
  label: string
): CsharpStarter | undefined {
  return csharpStarters.find((starter) => starter.label === label);
}
