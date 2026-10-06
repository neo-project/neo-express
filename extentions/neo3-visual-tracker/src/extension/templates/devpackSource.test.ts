import assert from "node:assert/strict";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const repoRoot = join(__dirname, "../../../../../");
const examplesRoot = join(repoRoot, "samples/examples");
const startersRoot = join(
  repoRoot,
  "extentions/neo3-visual-tracker/resources/new-contract/csharp-starters"
);

test("repo does not vendor Neo.SmartContract.Template contract sources", () => {
  assert.equal(existsSync(join(repoRoot, "scripts/sync-devpack-starters.mjs")), false);
  assert.equal(existsSync(join(examplesRoot, "devpack-source.json")), false);

  const exampleCs = existsSync(examplesRoot)
    ? readdirSync(examplesRoot, { recursive: true, encoding: "utf8" }).filter(
        (name) => name.endsWith(".cs")
      )
    : [];
  assert.deepEqual(exampleCs, []);

  for (const id of ["blank", "nep17", "nep11", "oracle", "ownable"]) {
    assert.equal(
      existsSync(join(startersRoot, id, "src", "$_CLASSNAME_$.cs.template.txt")),
      false,
      `${id} still vendors contract source`
    );
  }
});

test("samples/examples README installs Neo.SmartContract.Template", () => {
  const readme = readFileSync(join(examplesRoot, "README.md"), "utf8");
  assert.match(readme, /dotnet new install Neo\.SmartContract\.Template/);
  assert.match(readme, /neocontractnep17/);
  assert.match(readme, /neocontractnep11/);
  assert.match(readme, /neocontractoracle/);
  assert.match(readme, /neocontractowner/);
  assert.match(readme, /dotnet new neocontract/);
});
