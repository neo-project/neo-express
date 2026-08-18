export type InvocationAccount = {
  address: string;
  name: string;
};

export type WitnessScope = "CalledByEntry" | "Global" | "None";

export const witnessScopes: WitnessScope[] = [
  "CalledByEntry",
  "Global",
  "None",
];

export function toInvocationAccounts(addresses: {
  [name: string]: string;
}): InvocationAccount[] {
  return Object.entries(addresses)
    .map(([name, address]) => ({ address, name }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

export function resolveSelectedAccount(
  accounts: InvocationAccount[],
  selectedAccount: string | null
): string | null {
  if (accounts.some((account) => account.name === selectedAccount)) {
    return selectedAccount;
  }
  return accounts[0]?.name || null;
}

export function isWitnessScope(value: string): value is WitnessScope {
  return witnessScopes.includes(value as WitnessScope);
}

export function areInvocationStepsReady(
  steps: { contract?: string; operation?: string }[]
) {
  return (
    steps.length > 0 &&
    steps.every(
      (step) => !!step.contract?.trim() && !!step.operation?.trim()
    )
  );
}

export function isLiveDebugWitnessScopeSupported(scope: WitnessScope) {
  return scope === "CalledByEntry";
}
