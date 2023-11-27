export default function dedupeAndSort(xs: any[]) {
  const ys = xs.filter((value, index) => xs.indexOf(value) === index);
  ys.sort();
  return ys;
}
