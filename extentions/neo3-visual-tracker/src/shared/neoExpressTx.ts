export function looksLikeEncryptedWallet(account: string): boolean {
  return /\.json$/i.test(account);
}

export function parseGasBalance(showBalancesOutput: string): number | null {
  if (/no balances/i.test(showBalancesOutput)) {
    return 0;
  }
  const match = showBalancesOutput.match(
    /GAS\b[\s\S]*?balance:\s*([0-9]+(?:\.[0-9]+)?)/i
  );
  if (!match) {
    return null;
  }
  return Number.parseFloat(match[1]);
}

export function isFailedTx(message: string): boolean {
  return (
    /\bFAULT\b/i.test(message) ||
    /insufficient gas/i.test(message) ||
    /contract already exists/i.test(message)
  );
}

export function parseApplicationLogState(showTransactionOutput: string): string | null {
  const match = showTransactionOutput.match(/"vmstate"\s*:\s*"(\w+)"/i);
  return match ? match[1].toUpperCase() : null;
}

export function isNodeStillRunningError(message: string): boolean {
  return /currently running/i.test(message) || /already running/i.test(message);
}

export function parseSubmittedTxids(message: string): string[] {
  const matches = message.match(/0x[0-9a-f]{64}/gi) || [];
  return [...new Set(matches.map((id) => id.toLowerCase()))];
}

export function formatInvokeMessage(message: string): string {
  const cleaned = message.trim();
  if (!cleaned) {
    return "";
  }
  const start = cleaned.indexOf("{");
  const end = cleaned.lastIndexOf("}");
  if (start >= 0 && end > start) {
    try {
      const parsed = JSON.parse(cleaned.slice(start, end + 1));
      if (parsed && typeof parsed === "object") {
        const lines: string[] = [];
        if (parsed.state) {
          lines.push(`VM State: ${parsed.state}`);
        }
        if (parsed.gasconsumed) {
          lines.push(`Gas Consumed: ${parsed.gasconsumed}`);
        }
        if (parsed.exception) {
          lines.push(`Exception: ${parsed.exception}`);
        }
        if (Array.isArray(parsed.stack)) {
          lines.push("Result Stack:");
          lines.push(JSON.stringify(parsed.stack, null, 2));
        }
        if (lines.length) {
          return lines.join("\n");
        }
        return JSON.stringify(parsed, null, 2);
      }
    } catch {
      // Fall through and show the raw CLI output.
    }
  }
  return cleaned;
}

export function isContractOnChain(contractGetOutput: string): boolean {
  const text = contractGetOutput.trim();
  if (!text || text === "[]") {
    return false;
  }
  return /"name"\s*:/.test(text) || /hash:\s*0x/i.test(text);
}
