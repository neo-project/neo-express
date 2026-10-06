// Contract bodies in samples/examples and the VS Code C# starters are copies of
// neo-devpack-dotnet templates. This script refreshes them from the commit in
// samples/examples/devpack-source.json so the two trees cannot drift.
import { readFile, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const manifestPath = join(repoRoot, "samples/examples/devpack-source.json");
const check = process.argv.includes("--check");

function normalize(text) {
  const lf = text.replace(/\r\n/g, "\n");
  return lf.endsWith("\n") ? lf : `${lf}\n`;
}

function wizardSource(upstream, className) {
  return normalize(upstream)
    .replace(/namespace\s+[\w.]+/, () => "namespace $_CONTRACTNAME_$")
    .replace(
      new RegExp(`nameof\\(${className}\\)`),
      () => "nameof($_CLASSNAME_$)"
    )
    .replace(
      new RegExp(`class ${className}\\b`),
      () => "class $_CLASSNAME_$"
    );
}

async function loadUpstream(manifest, starter) {
  const url = `https://raw.githubusercontent.com/${manifest.repository}/${manifest.commit}/${starter.upstream}`;
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`GET ${url} failed: ${response.status}`);
  }
  return normalize(await response.text());
}

const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
const mismatches = [];

for (const starter of manifest.starters) {
  const upstream = await loadUpstream(manifest, starter);
  const wizardPath = join(
    repoRoot,
    "extentions/neo3-visual-tracker/resources/new-contract/csharp-starters",
    starter.id,
    "src",
    "$_CLASSNAME_$.cs.template.txt"
  );
  const examplePath = join(repoRoot, starter.example);
  const expected = [
    [examplePath, upstream],
    [wizardPath, wizardSource(upstream, starter.className)],
  ];

  for (const [path, content] of expected) {
    if (check) {
      const current = normalize(await readFile(path, "utf8"));
      if (current !== content) {
        mismatches.push(path);
      }
      continue;
    }
    await writeFile(path, content);
  }
}

if (mismatches.length > 0) {
  console.error(
    `DevPack starter sources differ from ${manifest.repository}@${manifest.commit}:`
  );
  for (const path of mismatches) {
    console.error(`  ${path}`);
  }
  console.error("Run: node scripts/sync-devpack-starters.mjs");
  process.exit(1);
}

if (check) {
  console.log(
    `C# starter sources match ${manifest.repository}@${manifest.commit}`
  );
}
