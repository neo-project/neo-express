import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const templatesSource = readFileSync(join(__dirname, "templates.ts"), "utf8");
const startersSource = readFileSync(
  join(__dirname, "csharpStarters.ts"),
  "utf8"
);

test("New contract wizard asks for a C# starter after language", () => {
  assert.match(templatesSource, /languageCode === "csharp"/);
  assert.match(templatesSource, /Contract template/);
  assert.match(templatesSource, /csharpStarterLabels\(\)/);
  assert.match(templatesSource, /csharpStarter\?\.overlay/);
  assert.match(
    templatesSource,
    /resources",\s+"new-contract",\s+"csharp-starters"/
  );
});

test("C# wizard starters include official Neo.SmartContract.Template overlays", () => {
  assert.match(startersSource, /NEP-17/);
  assert.match(startersSource, /NEP-11/);
  assert.match(startersSource, /Oracle/);
  assert.match(startersSource, /Ownable/);
  assert.match(startersSource, /overlay: true/);
});
