import assert from "node:assert/strict";
import { existsSync, promises as fs } from "node:fs";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import { createRequire } from "node:module";
import type { ExtensionContext } from "vscode";

const requireFromTest = createRequire(__filename);
const Module = requireFromTest("node:module") as {
  _load: (request: string, parent: unknown, isMain: boolean) => unknown;
};
const originalLoad = Module._load;
let root = "";
let generationFails = false;
const errors: string[] = [];
const successes: string[] = [];
const vscode = {
  workspace: { get workspaceFolders() { return [{ uri: { fsPath: root } }]; } },
  window: {
    showErrorMessage: (message: string) => errors.push(message),
    showInformationMessage: (message: string) => successes.push(message),
  },
};
Module._load = function(request: string, parent: unknown, isMain: boolean) {
  if (request === "vscode") return vscode;
  if (request === "./languages") return { languages: { csharp: { variables: {} } } };
  if (request === "./instantiateSmartContractTemplate") {
    return { instantiateOfficialContract: async () => {
      if (generationFails) throw new Error("Template installation failed");
    } };
  }
  return originalLoad.call(this, request, parent, isMain);
};
let Templates: typeof import("./templates").default;
let IoHelpers: typeof import("../util/ioHelpers").default;
try {
  Templates = requireFromTest("./templates").default;
  IoHelpers = requireFromTest("../util/ioHelpers").default;
} finally {
  Module._load = originalLoad;
}

for (const failure of ["generation", "scaffold", "overlay"]) {
  test(`Failed ${failure} can be retried without leaving a partial contract`, async (t) => {
    root = await mkdtemp(join(tmpdir(), "neoxp-wizard-retry-"));
    const extensionPath = join(root, "extension");
    const scaffold = join(extensionPath, "resources/new-contract/csharp");
    const overlay = join(extensionPath, "resources/new-contract/csharp-starters/blank");
    const destination = join(root, "contracts/Example");
    errors.length = 0;
    successes.length = 0;
    generationFails = failure === "generation";
    t.mock.method(IoHelpers, "multipleChoice", async (prompt: string) =>
      prompt === "Contract template" ? "Blank contract" : "C# (recommended for N3)");
    t.mock.method(Templates as unknown as { gatherParameters: () => Promise<Record<string, string>> }, "gatherParameters", async () => ({
      "$_CONTRACTNAME_$": "Example", "$_CLASSNAME_$": "ExampleContract",
    }));
    try {
      if (failure !== "scaffold") {
        await mkdir(scaffold, { recursive: true });
        await writeFile(join(scaffold, "source.txt"), "scaffold");
      }
      if (failure !== "overlay") await mkdir(overlay, { recursive: true });
      await Templates.newContract({ extensionPath } as ExtensionContext);
      assert.equal(successes.length, 0);
      assert.equal(errors.length, 1);
      assert.equal(existsSync(destination), false, "Failed creation must release its destination");
      generationFails = false;
      await mkdir(scaffold, { recursive: true });
      await writeFile(join(scaffold, "source.txt"), "scaffold");
      await mkdir(overlay, { recursive: true });
      await Templates.newContract({ extensionPath } as ExtensionContext);
      assert.equal(successes.length, 1, "The same contract name must be reusable");
      assert.equal(await readFile(join(destination, "source.txt"), "utf8"), "scaffold");
    } finally {
      await rm(root, { recursive: true, force: true });
    }
  });
}

test("An existing contract is preserved when creation is rejected", async (t) => {
  root = await mkdtemp(join(tmpdir(), "neoxp-wizard-existing-"));
  const destination = join(root, "contracts/Example");
  t.mock.method(IoHelpers, "multipleChoice", async (prompt: string) =>
    prompt === "Contract template" ? "Blank contract" : "C# (recommended for N3)");
  t.mock.method(Templates as unknown as { gatherParameters: () => Promise<Record<string, string>> }, "gatherParameters", async () => ({ "$_CONTRACTNAME_$": "Example" }));
  try {
    await mkdir(destination, { recursive: true });
    await writeFile(join(destination, "existing.txt"), "keep");
    await Templates.newContract({ extensionPath: root } as ExtensionContext);
    assert.equal(await readFile(join(destination, "existing.txt"), "utf8"), "keep");
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});


test("A destination created concurrently is preserved", async (t) => {
  root = await mkdtemp(join(tmpdir(), "neoxp-wizard-race-"));
  const destination = join(root, "contracts/Example");
  const originalMkdir = fs.mkdir;
  errors.length = 0;
  successes.length = 0;
  t.mock.method(IoHelpers, "multipleChoice", async (prompt: string) =>
    prompt === "Contract template" ? "Blank contract" : "C# (recommended for N3)");
  t.mock.method(Templates as unknown as { gatherParameters: () => Promise<Record<string, string>> }, "gatherParameters", async () => ({ "$_CONTRACTNAME_$": "Example" }));
  t.mock.method(fs, "mkdir", async (path: Parameters<typeof fs.mkdir>[0], options?: Parameters<typeof fs.mkdir>[1]) => {
    if (path === destination) {
      await originalMkdir(destination);
      await writeFile(join(destination, "existing.txt"), "other creator");
    }
    return originalMkdir(path, options);
  });
  try {
    await Templates.newContract({ extensionPath: root } as ExtensionContext);
    assert.equal(successes.length, 0);
    assert.equal(errors.length, 1);
    assert.equal(await readFile(join(destination, "existing.txt"), "utf8"), "other creator");
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
