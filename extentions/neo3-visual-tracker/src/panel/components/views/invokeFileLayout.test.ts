import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const styles = readFileSync(join(__dirname, "../../styles.css"), "utf8");
const editor = readFileSync(
  join(__dirname, "../contracts/InvokeFileInteractiveEditor.tsx"),
  "utf8"
);

test("contract studio content scrolls on short viewports", () => {
  assert.match(styles, /html\[data-view="invokeFile"\] \.panel-shell/);
  assert.match(
    styles,
    /\.contract-studio__content \{[\s\S]*overflow:\s*auto/
  );
  assert.doesNotMatch(
    styles,
    /grid-template-rows:\s*minmax\(320px,\s*1fr\)\s*minmax\(180px,\s*0\.55fr\)/
  );
});

test("invocation results stay in the studio until dismissed", () => {
  assert.match(editor, /dismissInvocationResult/);
  assert.match(editor, /InvocationResultView/);
  assert.match(editor, /showInvocationResult/);
});

test("invocation opens the Output channel to the results", () => {
  const controller = readFileSync(
    join(
      __dirname,
      "../../../extension/panelControllers/invokeFilePanelController.ts"
    ),
    "utf8"
  );
  const log = readFileSync(
    join(__dirname, "../../../extension/util/log.ts"),
    "utf8"
  );
  assert.match(log, /createOutputChannel\(\s*"Neo Express Invocation"/);
  assert.match(log, /ensureInvocationChannel\(\)\.show\(/);
  assert.match(controller, /Log\.showInvocation\(/);
  assert.match(controller, /Log\.writeInvocation\(/);
});
