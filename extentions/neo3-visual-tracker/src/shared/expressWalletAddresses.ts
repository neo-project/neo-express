export function isHiddenExpressConfig(filePath: string): boolean {
  const normalized = filePath.replace(/\\/g, "/").toLowerCase();
  return (
    normalized.includes("/test/") ||
    normalized.includes("/tests/") ||
    normalized.includes("/obj/") ||
    normalized.includes("/bin/") ||
    normalized.endsWith("tests.neo-express")
  );
}

export function parseExpressWalletAddresses(config: {
  wallets?: Array<{
    name?: string;
    accounts?: Array<{
      label?: string | null;
      "is-default"?: boolean;
      "script-hash"?: string;
    }>;
  }>;
  "consensus-nodes"?: Array<{
    wallet?: {
      name?: string;
      accounts?: Array<{
        label?: string | null;
        "is-default"?: boolean;
        "script-hash"?: string;
      }>;
    };
  }>;
}): { [name: string]: string } {
  const result: { [name: string]: string } = {};
  const add = (name: string | undefined, scriptHash: string | undefined) => {
    if (!name || !scriptHash || result[name]) {
      return;
    }
    result[name] = scriptHash;
  };

  for (const wallet of config.wallets ?? []) {
    const accounts = wallet.accounts ?? [];
    const defaultAccount =
      accounts.find((account) => account["is-default"]) ?? accounts[0];
    add(wallet.name, defaultAccount?.["script-hash"]);
    for (const account of accounts) {
      if (account.label) {
        add(account.label, account["script-hash"]);
      }
    }
  }

  for (const node of config["consensus-nodes"] ?? []) {
    const wallet = node.wallet;
    const accounts = wallet?.accounts ?? [];
    for (const account of accounts) {
      if (account.label === "Consensus MultiSigContract") {
        add("genesis", account["script-hash"]);
      } else if (account["is-default"] || !account.label) {
        add(wallet?.name, account["script-hash"]);
      } else {
        add(account.label, account["script-hash"]);
      }
    }
  }

  return result;
}

export function workspaceWalletDisplayName(
  filePath: string,
  accountLabel?: string | null
): string {
  const slash = Math.max(filePath.lastIndexOf("/"), filePath.lastIndexOf("\\"));
  const fileName = slash >= 0 ? filePath.slice(slash + 1) : filePath;
  const base = fileName
    .replace(/\.neo-wallet\.json$/i, "")
    .replace(/\.json$/i, "");
  if (accountLabel && accountLabel !== "Default account") {
    return accountLabel;
  }
  return base || accountLabel || "wallet";
}

export function contractsWithStandard(
  manifests: { [name: string]: { supportedstandards?: string[] } },
  standard: string
): string[] {
  const needle = standard.replace("-", "").toUpperCase();
  const names = new Set<string>();
  for (const [name, manifest] of Object.entries(manifests)) {
    if (name.startsWith("0x")) {
      continue;
    }
    const standards = manifest.supportedstandards ?? [];
    if (
      standards.some(
        (entry) => entry.replace("-", "").toUpperCase() === needle
      )
    ) {
      names.add(name);
    }
  }
  return [...names].sort((a, b) => a.localeCompare(b));
}

export function workspaceNep6AccountNames(accountSigners?: {
  [name: string]: string;
}): string[] {
  if (!accountSigners) {
    return [];
  }
  return Object.keys(accountSigners).filter(
    (name) => accountSigners[name] !== name
  );
}

export function resolveAccountForIdentifier(
  displayName: string,
  signer: string,
  identifierWallets: { [name: string]: string }
): string {
  return identifierWallets[displayName] || signer;
}

export type AccountChoice = { label: string; signer: string; address: string };

export function walletFileName(filePath: string): string {
  const slash = Math.max(filePath.lastIndexOf("/"), filePath.lastIndexOf("\\"));
  return slash >= 0 ? filePath.slice(slash + 1) : filePath;
}

export function sortWalletNames(names: string[]): string[] {
  return [...names].sort((a, b) => {
    if (a === "genesis") {
      return -1;
    }
    if (b === "genesis") {
      return 1;
    }
    return a.localeCompare(b);
  });
}

export function buildAccountChoices(
  expressWallets: { [name: string]: string },
  accountSigners?: { [name: string]: string },
  knownAddresses?: { [name: string]: string }
): AccountChoice[] {
  const expressNames = sortWalletNames(Object.keys(expressWallets));
  const expressSet = new Set(expressNames);
  const choices: AccountChoice[] = expressNames.map((name) => ({
    label: name,
    signer: name,
    address: expressWallets[name],
  }));
  for (const name of sortWalletNames(workspaceNep6AccountNames(accountSigners))) {
    const path = accountSigners![name];
    const label = expressSet.has(name)
      ? `${name} (${walletFileName(path)})`
      : name;
    choices.push({
      label,
      signer: path,
      address: knownAddresses?.[name] || "",
    });
  }
  return choices;
}

export function addressForAccountChoice(
  label: string,
  choices: AccountChoice[]
): string {
  return choices.find((choice) => choice.label === label)?.address || label;
}

export function signerForAccountChoice(
  label: string,
  choices: AccountChoice[]
): string {
  return choices.find((choice) => choice.label === label)?.signer || label;
}
