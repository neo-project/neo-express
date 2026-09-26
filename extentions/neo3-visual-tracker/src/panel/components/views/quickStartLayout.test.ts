import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const packageRoot = join(__dirname, "../../../..");
const packageJson = JSON.parse(
  readFileSync(join(packageRoot, "package.json"), "utf8")
) as {
  contributes?: {
    views?: {
      [container: string]: { id?: string; type?: string }[];
    };
  };
};
const indexSource = readFileSync(
  join(packageRoot, "src/extension/index.ts"),
  "utf8"
);

test("Quick Start is the first sidebar view so the last action is not clipped", () => {
  const views =
    packageJson.contributes?.views?.["neo3-visual-devtracker-mainView"] ?? [];
  assert.equal(views[0]?.id, "neo3-visual-devtracker.views.quickStart");
  const tree = readFileSync(
    join(packageRoot, "src/extension/vscodeProviders/quickStartTreeDataProvider.ts"),
    "utf8"
  );
  assert.match(tree, /if \(!item\.command\)/);
});

test("Quick Start is a tree view so it uses the main sidebar scrollbar", () => {
  const views =
    packageJson.contributes?.views?.["neo3-visual-devtracker-mainView"] ?? [];
  const quickStart = views.find(
    (view) => view.id === "neo3-visual-devtracker.views.quickStart"
  );
  assert.ok(quickStart, "Quick Start view is missing");
  assert.notEqual(
    quickStart.type,
    "webview",
    "Quick Start must not be a webview (those get a nested scrollbar)"
  );
  assert.match(
    indexSource,
    /registerTreeDataProvider\(\s*"neo3-visual-devtracker\.views\.quickStart"/
  );
  assert.doesNotMatch(indexSource, /registerWebviewViewProvider/);
});
