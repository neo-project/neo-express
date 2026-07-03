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
  assert.match(csharpTemplate, /neoxp create -o &quot;\$\(NeoExpressBatchInputFile\)&quot;/);
  assert.match(csharpTemplate, /neoxp wallet create -i &quot;\$\(NeoExpressBatchInputFile\)&quot; owner/);
});
