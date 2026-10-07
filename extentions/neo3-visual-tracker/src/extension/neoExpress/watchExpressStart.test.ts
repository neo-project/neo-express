import assert from "node:assert/strict";
import test from "node:test";

import { watchForExpressStart } from "./watchExpressStart";

test("watchForExpressStart registers write handlers before any output", async () => {
  const writeListeners: ((data: string) => void)[] = [];
  const exitListeners: ((code: number | null) => void)[] = [];
  const pty = {
    onDidWrite(listener: (data: string) => void) {
      writeListeners.push(listener);
    },
    onDidExit(listener: (code: number | null) => void) {
      exitListeners.push(listener);
    },
  };

  const started = watchForExpressStart(pty, 1000);
  assert.equal(writeListeners.length, 1, "handler must be registered immediately");
  writeListeners[0]("Neo express is running\n");
  assert.equal(await started, true);
});

test("watchForExpressStart does not miss a fast startup line", async () => {
  const writeListeners: ((data: string) => void)[] = [];
  const pty = {
    onDidWrite(listener: (data: string) => void) {
      writeListeners.push(listener);
    },
    onDidExit() {},
  };

  const started = watchForExpressStart(pty, 50);
  writeListeners[0]("Neo express is running");
  assert.equal(await started, true);
});
