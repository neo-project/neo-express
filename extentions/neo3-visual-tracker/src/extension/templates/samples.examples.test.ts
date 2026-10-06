import assert from "node:assert/strict";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const examplesRoot = join(
  __dirname,
  "../../../../../samples/examples"
);

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
