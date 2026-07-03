import { strict as assert } from "node:assert";
import test from "node:test";

import { getQuickStartActions } from "./quickStartActions";
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

test("offers Neo Express wallet creation when an instance exists", () => {
  const actions = getQuickStartActions({
    ...baseState,
    hasNeoExpressInstance: true,
  });
  assert(actions.includes("createExpressWallet"));
});

test("offers transfer when Express and wallets are available", () => {
  const actions = getQuickStartActions({
    ...baseState,
    hasNeoExpressInstance: true,
    hasWallets: true,
  });
  assert(actions.includes("transfer"));
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
