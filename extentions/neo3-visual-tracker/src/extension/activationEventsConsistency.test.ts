import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const packageJson = JSON.parse(
  readFileSync(join(__dirname, "../../package.json"), "utf8")
) as {
  activationEvents?: string[];
  contributes?: { customEditors?: { viewType?: string }[] };
};

test("every onCustomEditor activation event matches a contributed customEditor viewType", () => {
  const viewTypes = new Set(
    (packageJson.contributes?.customEditors ?? [])
      .map((editor) => editor.viewType)
      .filter((viewType): viewType is string => typeof viewType === "string")
  );

  const customEditorEvents = (packageJson.activationEvents ?? []).filter(
    (event) => event.startsWith("onCustomEditor:")
  );

  for (const event of customEditorEvents) {
    const viewType = event.slice("onCustomEditor:".length);
    assert.ok(
      viewTypes.has(viewType),
      `activationEvents entry "${event}" has no matching contributes.customEditors viewType`
    );
  }
});
