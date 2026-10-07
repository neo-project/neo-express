const ANSI_RE = new RegExp(`${String.fromCharCode(0x1b)}\\[[0-9;]*m`, "g");

/** Strip VT/ANSI color codes from neoxp CLI output before showing VS Code toasts. */
export default function stripAnsi(text: string): string {
  return text.replace(ANSI_RE, "").trim();
}
