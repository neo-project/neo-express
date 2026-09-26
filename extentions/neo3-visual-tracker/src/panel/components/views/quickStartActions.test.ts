import { strict as assert } from "node:assert";
import test from "node:test";

import {
  describeQuickStartAction,
  getQuickStartActions,
} from "./quickStartActions";
import QuickStartViewState from "../../../shared/viewState/quickStartViewState";

const baseState: QuickStartViewState = {
  view: "quickStart",
  panelTitle: "",
  connectionName: null,
  hasContracts: false,
  hasDeployedContract: false,
  hasNeoExpressInstance: false,
  hasWallets: false,
  hasCheckpoints: false,
  hasCheckpointCompatibleNeoExpressInstance: false,
  neoDeploymentRequired: false,
  neoExpressDeploymentRequired: false,
  neoExpressIsRunning: false,
  workspaceIsOpen: true,
};

test("offers NEP-6 wallet creation when none exist", () => {
  const actions = getQuickStartActions(baseState);
  assert(actions.includes("createWallet"));
});

test("always offers new contract when a workspace is open", () => {
  const empty = getQuickStartActions(baseState);
  assert(empty.includes("createContract"));
  const withContracts = getQuickStartActions({
    ...baseState,
    hasContracts: true,
  });
  assert(withContracts.includes("createContract"));
});

test("offers Neo Express wallet creation when an instance exists", () => {
  const actions = getQuickStartActions({
    ...baseState,
    hasNeoExpressInstance: true,
  });
  assert(actions.includes("createExpressWallet"));
  assert(actions.includes("resetExpress"));
});

test("offers deploy when connected with workspace contracts", () => {
  const actions = getQuickStartActions({
    ...baseState,
    connectionName: "default.neo-express",
    hasContracts: true,
    hasDeployedContract: true,
    hasNeoExpressInstance: true,
  });
  assert(actions.includes("deployExpressContract"));
  assert(actions.includes("invokeContract"));
  assert.equal(
    actions.filter((action) => action === "deployExpressContract").length,
    1
  );
});

test("offers transfer when Express and wallets are available", () => {
  const actions = getQuickStartActions({
    ...baseState,
    hasNeoExpressInstance: true,
    hasWallets: true,
  });
  assert(actions.includes("transfer"));
  assert(actions.includes("transferNft"));
});

test("offers transfer in a fresh Express workspace that has no NEP-6 wallets", () => {
  const actions = getQuickStartActions({
    ...baseState,
    hasNeoExpressInstance: true,
    hasWallets: false,
  });
  assert(actions.includes("transfer"));
  assert(actions.includes("transferNft"));
  assert(actions.includes("createWallet"));
});

test("does not offer checkpoint actions when no single-node Express instance is present", () => {
  const actions = getQuickStartActions({
    ...baseState,
    hasNeoExpressInstance: true,
    hasCheckpoints: true,
  });
  assert(!actions.includes("createCheckpoint"));
  assert(!actions.includes("restoreCheckpoint"));
});

test("every Quick Start action maps to a sidebar command", () => {
  const actions = getQuickStartActions(baseState);
  for (const action of actions) {
    const item = describeQuickStartAction(action, baseState);
    assert.equal(item.action, action);
    assert.ok(item.label.length > 0);
    assert.ok(item.command.startsWith("neo3-visual-devtracker.") || item.command === "vscode.openFolder");
  }
});

test("offers checkpoint actions when a single-node Express instance is present", () => {
  const actions = getQuickStartActions({
    ...baseState,
    hasNeoExpressInstance: true,
    hasCheckpointCompatibleNeoExpressInstance: true,
    hasCheckpoints: true,
  });
  assert(actions.includes("createCheckpoint"));
  assert(actions.includes("restoreCheckpoint"));
});
