export default function sameFilePath(left: string, right: string) {
  return normalizeForComparison(left) === normalizeForComparison(right);
}

function normalizeForComparison(filePath: string) {
  const normalized = filePath.replace(/\\/g, "/");
  return /^[A-Za-z]:\//.test(normalized) ? normalized.toLowerCase() : normalized;
}
