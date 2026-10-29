import assert from "node:assert/strict";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const examplesRoot = join(
  __dirname,
  "../../../../../samples/examples"
);
const repoRoot = join(examplesRoot, "..", "..");

test("samples/examples documents official templates instead of copies", () => {
  const readme = readFileSync(join(examplesRoot, "README.md"), "utf8");
  assert.match(readme, /dotnet new install Neo\.SmartContract\.Template/);
  assert.match(readme, /neocontractnep17/);
  assert.match(readme, /neoxp contract deploy/);
  assert.equal(existsSync(join(examplesRoot, "examples.sln")), false);
  assert.equal(existsSync(join(examplesRoot, "Directory.Build.props")), false);

  const names = readdirSync(examplesRoot);
  assert.deepEqual(
    names.filter((name) => !name.startsWith(".")),
    ["README.md"]
  );
});

test("getting-started walkthrough uses official templates and a source-built neoxp", () => {
  const gettingStarted = readFileSync(
    join(repoRoot, "docs", "getting-started.md"),
    "utf8"
  );
  const quickstart = readFileSync(
    join(repoRoot, "docs", "quickstart.md"),
    "utf8"
  );

  assert.match(gettingStarted, /#use-the-command-line/);
  assert.match(gettingStarted, /REPO_ROOT=/);
  assert.match(gettingStarted, /\$repoRoot/);
  assert.match(gettingStarted, /dotnet new install Neo\.SmartContract\.Template/);
  assert.match(gettingStarted, /dotnet new neocontractnep17/);
  assert.doesNotMatch(gettingStarted, /dotnet exec "\$\(pwd\)\/src\/neoxp/);
  assert.doesNotMatch(gettingStarted, /cd samples\/examples\/Nep17/);
  assert.doesNotMatch(gettingStarted, /transfer\.neo-invoke\.json/);

  assert.match(quickstart, /REPO_ROOT=/);
  assert.match(quickstart, /\$repoRoot/);
  assert.match(quickstart, /dotnet new neocontractnep17/);
  assert.doesNotMatch(quickstart, /cd samples\/examples\/Nep17/);
  assert.doesNotMatch(quickstart, /dotnet build samples\/examples\/Nep17/);
});
