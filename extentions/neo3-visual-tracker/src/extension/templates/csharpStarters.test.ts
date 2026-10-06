import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

import {
  csharpStarterLabels,
  csharpStarters,
  findCsharpStarter,
} from "./csharpStarters";

const packageRoot = join(__dirname, "../../..");
const startersRoot = join(packageRoot, "resources/new-contract/csharp-starters");
const csharpRoot = join(packageRoot, "resources/new-contract/csharp");

test("C# starter catalog matches Neo.SmartContract.Template plus storage", () => {
  assert.deepEqual(
    csharpStarters.map((starter) => starter.id),
    ["blank", "nep17", "nep11", "oracle", "ownable", "storage"]
  );
  assert.deepEqual(csharpStarterLabels(), [
    "Blank contract",
    "NEP-17 token",
    "NEP-11 NFT",
    "Oracle",
    "Ownable",
    "Storage (number map)",
  ]);
  assert.equal(findCsharpStarter("NEP-17 token")?.id, "nep17");
  assert.equal(findCsharpStarter("NEP-17 token")?.template, "neocontractnep17");
  assert.equal(findCsharpStarter("Blank contract")?.template, "neocontract");
  assert.equal(findCsharpStarter("Ownable")?.template, "neocontractowner");
  assert.equal(findCsharpStarter("Storage (number map)")?.overlay, false);
  assert.equal(findCsharpStarter("Storage (number map)")?.template, undefined);
  assert.equal(findCsharpStarter("missing"), undefined);
});

test("overlay C# starters ship neo-express tests and not contract source", () => {
  for (const starter of csharpStarters.filter((item) => item.overlay)) {
    const source = join(
      startersRoot,
      starter.id,
      "src",
      "$_CLASSNAME_$.cs.template.txt"
    );
    const tests = join(
      startersRoot,
      starter.id,
      "test",
      "$_CLASSNAME_$Tests.cs.template.txt"
    );
    assert.equal(existsSync(source), false, `vendored ${source}`);
    assert.equal(existsSync(tests), true, `missing ${tests}`);
    assert.ok(starter.template, `${starter.id} must map to a dotnet new short name`);
  }
});

test("storage starter stays the default C# scaffold", () => {
  const source = join(csharpRoot, "src", "$_CLASSNAME_$.cs.template.txt");
  const tests = join(csharpRoot, "test", "$_CLASSNAME_$Tests.cs.template.txt");
  const tools = join(csharpRoot, ".config", "dotnet-tools.json");
  const csproj = join(csharpRoot, "src", "$_CLASSNAME_$.csproj.template.txt");
  assert.equal(existsSync(source), true);
  assert.equal(existsSync(tests), true);
  assert.equal(existsSync(tools), true);
  const sourceText = readFileSync(source, "utf8");
  assert.match(sourceText, /ChangeNumber/);
  const toolsJson = JSON.parse(readFileSync(tools, "utf8"));
  assert.equal(toolsJson.tools["neo.express"].version, "3.10.1");
  assert.equal(toolsJson.tools["neo.compiler.csharp"].version, "3.10.1");
  const csprojText = readFileSync(csproj, "utf8");
  assert.match(csprojText, /Neo\.BuildTasks/);
  assert.match(csprojText, /NeoContractName>\$_CLASSNAME_\$/);
});

test("csharp-starters contains only cataloged overlay ids", () => {
  const ids = readdirSync(startersRoot).sort();
  assert.deepEqual(
    ids,
    csharpStarters
      .filter((starter) => starter.overlay)
      .map((starter) => starter.id)
      .sort()
  );
});
