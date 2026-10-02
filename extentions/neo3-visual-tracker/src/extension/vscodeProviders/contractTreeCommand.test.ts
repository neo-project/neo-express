import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

import getContractTreeCommand, {
  ContractTreeItemData,
  getWorkspaceContractPath,
} from "./contractTreeCommand";

test("getContractTreeCommand opens Contract Studio for deployed contracts", () => {
  const contract = { name: "Sample", hash: "0x1234" };
  assert.deepEqual(getContractTreeCommand(contract), {
    command: "neo3-visual-devtracker.neo.openContractStudio",
    arguments: [contract],
    title: "Invoke in Contract Studio",
  });
});

test("getContractTreeCommand has no default command for workspace contracts", () => {
  const contract = { name: "Sample", path: "/workspace/Sample.nef" };
  assert.equal(getContractTreeCommand(contract), undefined);
});

test("workspace contracts keep the explicit rocket action in package.json", () => {
  const packageJson = JSON.parse(
    readFileSync(join(__dirname, "../../../package.json"), "utf8")
  );
  const inline = packageJson.contributes.menus["view/item/context"].filter(
    (item: { command: string; when?: string; group?: string }) =>
      item.command === "neo3-visual-devtracker.neo.openContractStudio"
  );
  assert.ok(
    inline.some(
      (item: { when?: string; group?: string }) =>
        item.when?.includes("workspaceContract") && item.group === "inline"
    )
  );
});

test("workspace contract tree items expose their trusted file path", () => {
  const item = new ContractTreeItemData(
    "Sample",
    undefined,
    undefined,
    "/workspace/Sample.nef"
  );

  assert.equal(getWorkspaceContractPath(item), "/workspace/Sample.nef");
});

test("plain command arguments cannot supply a trusted workspace path", () => {
  assert.equal(
    getWorkspaceContractPath({
      name: "Sample",
      path: "/outside-workspace/Sample.nef",
    }),
    undefined
  );
});
