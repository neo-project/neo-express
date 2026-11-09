export type CsharpStarter = {
  id: string;
  label: string;
  /** Overlay Express test files from csharp-starters/<id> after the C# scaffold. */
  overlay: boolean;
  /** `dotnet new` short name from Neo.SmartContract.Template. */
  template?: string;
};

/**
 * C# starters from Neo.SmartContract.Template (`dotnet new install`).
 * Official unit-test projects are not copied; Express tests overlay instead.
 */
export const csharpStarters: CsharpStarter[] = [
  {
    id: "blank",
    label: "Blank contract",
    overlay: true,
    template: "neocontract",
  },
  {
    id: "nep17",
    label: "NEP-17 token",
    overlay: true,
    template: "neocontractnep17",
  },
  {
    id: "nep11",
    label: "NEP-11 NFT",
    overlay: true,
    template: "neocontractnep11",
  },
  {
    id: "oracle",
    label: "Oracle",
    overlay: true,
    template: "neocontractoracle",
  },
  {
    id: "ownable",
    label: "Ownable",
    overlay: true,
    template: "neocontractowner",
  },
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
