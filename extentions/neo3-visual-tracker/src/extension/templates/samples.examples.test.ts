import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const examplesRoot = join(
  __dirname,
  "../../../../../samples/examples"
);

const expected = [
  { folder: "Blank", contract: "Contract", batch: "Contract.nef" },
  { folder: "Nep17", contract: "Nep17Contract", batch: "Nep17Contract.nef" },
  { folder: "Nep11", contract: "Nep11Contract", batch: "Nep11Contract.nef" },
  { folder: "Oracle", contract: "OracleRequest", batch: "OracleRequest.nef" },
  { folder: "Ownable", contract: "Ownable", batch: "Ownable.nef" },
];

test("samples/examples uses a neo-express + BuildTasks layout", () => {
  const props = readFileSync(
    join(examplesRoot, "Directory.Build.props"),
    "utf8"
  );
  assert.match(props, /Neo\.BuildTasks/);
  assert.match(props, /DeployContractToNeoExpress/);
  assert.match(props, /contract deploy/);
  assert.match(props, /NeoExpressCli/);
  assert.match(props, /Configuration Condition="'\$\(Configuration\)'==''">Debug/);
  assert.match(props, /src\\neoxp\\neoxp\.csproj|src\/neoxp\/neoxp\.csproj/);
  assert.match(props, /_NeoDeployForce/);
  assert.match(props, / --force/);
  assert.doesNotMatch(
    props,
    /IgnoreExitCode="true"\s+Command="\$\(NeoExpressCli\) contract deploy/
  );
  assert.equal(existsSync(join(examplesRoot, "README.md")), true);

  for (const example of expected) {
    const root = join(examplesRoot, example.folder);
    const csproj = readdirSync(root).find((name) => name.endsWith(".csproj"));
    assert.ok(csproj, `${example.folder} is missing a root csproj for 'dotnet build'`);
    const csprojText = readFileSync(join(root, csproj!), "utf8");
    assert.match(
      csprojText,
      new RegExp(`<NeoContractName>${example.contract}</NeoContractName>`)
    );
    const batch = readFileSync(join(root, "express.batch"), "utf8");
    assert.match(batch, new RegExp(`contract deploy .*${example.batch} genesis`));
    assert.equal(
      existsSync(join(root, "src", `${example.contract}.cs`)) ||
        readdirSync(join(root, "src")).some((name) => name.endsWith(".cs")),
      true
    );
    const sources = readdirSync(join(root, "src")).filter((name) =>
      name.endsWith(".cs")
    );
    for (const source of sources) {
      const text = readFileSync(join(root, "src", source), "utf8");
      assert.doesNotMatch(text, /NUnit|MSTest|Xunit/, `${source} pulled in tests`);
    }
  }
});
