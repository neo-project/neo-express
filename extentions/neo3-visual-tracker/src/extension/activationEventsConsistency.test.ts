import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const packageJson = JSON.parse(
  readFileSync(join(__dirname, "../../package.json"), "utf8")
) as {
  activationEvents?: string[];
  contributes?: {
    commands?: { command?: string }[];
    customEditors?: { viewType?: string }[];
  };
};

test("every onCommand activation event matches a contributed command", () => {
  const commands = new Set(
    (packageJson.contributes?.commands ?? [])
      .map((entry) => entry.command)
      .filter((command): command is string => typeof command === "string")
  );

  const commandEvents = (packageJson.activationEvents ?? []).filter((event) =>
    event.startsWith("onCommand:")
  );

  for (const event of commandEvents) {
    const command = event.slice("onCommand:".length);
    assert.ok(
      commands.has(command),
      `activationEvents entry "${event}" has no matching contributes.commands entry`
    );
  }
});

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
