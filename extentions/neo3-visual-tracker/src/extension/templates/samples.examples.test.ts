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

test("getting-started walkthrough matches the repo layout without --configuration", () => {
  const gettingStarted = readFileSync(
    join(repoRoot, "docs", "getting-started.md"),
    "utf8"
  );
  const quickstart = readFileSync(
    join(repoRoot, "docs", "quickstart.md"),
    "utf8"
  );
  const props = readFileSync(
    join(examplesRoot, "Directory.Build.props"),
    "utf8"
  );

  assert.match(gettingStarted, /#use-the-command-line/);
  assert.match(gettingStarted, /REPO_ROOT=/);
  assert.match(gettingStarted, /\$repoRoot/);
  assert.doesNotMatch(gettingStarted, /dotnet exec "\$\(pwd\)\/src\/neoxp/);
  assert.doesNotMatch(gettingStarted, /transfer\.neo-invoke\.json/);
  assert.match(gettingStarted, /invoke-files\/symbol\.neo-invoke\.json/);

  assert.match(quickstart, /REPO_ROOT=/);
  assert.match(quickstart, /\$repoRoot/);
  assert.match(quickstart, /cd samples\/examples\/Nep17/);
  assert.match(quickstart, /```shell\r?\ndotnet build\r?\n```/);
  assert.doesNotMatch(quickstart, /dotnet build samples\/examples\/Nep17/);

  assert.match(props, /Configuration Condition="'\$\(Configuration\)'==''">Debug/);
  assert.doesNotMatch(props, /bin\/\/net10\.0/);

  const invoke = JSON.parse(
    readFileSync(
      join(examplesRoot, "Nep17", "invoke-files", "symbol.neo-invoke.json"),
      "utf8"
    )
  );
  assert.equal(invoke[0].contract, "Nep17Contract");
  assert.equal(invoke[0].operation, "symbol");
});
