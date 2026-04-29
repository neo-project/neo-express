export default function getTrustedWorkspaceServerListFiles(
  files: string[],
  isTrusted: boolean
): string[] {
  return isTrusted ? [...files] : [];
}
