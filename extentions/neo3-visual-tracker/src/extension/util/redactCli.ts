export function redactCliArgs(args: string[]): string[] {
  const redacted = [...args];
  for (let i = 0; i < redacted.length; i++) {
    const token = redacted[i];
    if (token === "--password" || token === "-p") {
      if (i + 1 < redacted.length) {
        redacted[i + 1] = "********";
        i++;
      }
      continue;
    }
    if (token.startsWith("--password=") || token.startsWith("-p=")) {
      const eq = token.indexOf("=");
      redacted[i] = `${token.slice(0, eq + 1)}********`;
    }
  }
  return redacted;
}

export function formatCli(command: string, args: string[]): string {
  return [command, ...redactCliArgs(args)].join(" ").trim();
}
