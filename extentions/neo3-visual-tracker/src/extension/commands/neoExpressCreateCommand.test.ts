import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const packageRoot = join(__dirname, "../../..");

test("Visual Tracker create commands pass Neo Express output explicitly", () => {
  const neoExpressCommands = readFileSync(
    join(__dirname, "neoExpressCommands.ts"),
    "utf8"
  );
  assert.match(
    neoExpressCommands,
    /"create",\s+"-f",\s+"-c",\s+nodeCount,\s+"-o",\s+configSavePath/s
  );

  const languages = readFileSync(
    join(__dirname, "../templates/languages.ts"),
    "utf8"
  );
  assert.match(
    languages,
    /\["create",\s+"-f",\s+"-o",\s+"test\/\$_CONTRACTNAME_\$Tests\.neo-express"\]/
  );

  const csharpTemplate = readFileSync(
    join(
      packageRoot,
      "resources/new-contract/csharp/test/$_CLASSNAME_$Tests.csproj.template.txt"
    ),
    "utf8"
  );
  assert.match(csharpTemplate, /dotnet tool run neoxp -- create -o default\.neo-express/);
  assert.match(csharpTemplate, /dotnet tool run neoxp -- wallet create -i default\.neo-express owner/);
  assert.match(csharpTemplate, /NeoExpressBatchInputFile>\.\.\/default\.neo-express/);
  assert.doesNotMatch(csharpTemplate, /Tests\.neo-express/);
});
