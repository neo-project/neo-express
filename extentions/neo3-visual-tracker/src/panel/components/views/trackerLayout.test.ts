import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";

const tracker = readFileSync(join(__dirname, "Tracker.tsx"), "utf8");
const styles = readFileSync(join(__dirname, "../../styles.css"), "utf8");

test("block explorer list scrolls inside the panel on small screens", () => {
  assert.match(tracker, /className="tracker__blocks"/);
  assert.doesNotMatch(tracker, /minHeight:\s*"100vh"/);
  assert.match(styles, /\.tracker__blocks \{[\s\S]*overflow:\s*auto/);
  assert.match(styles, /html\[data-view="tracker"\] \.panel-shell/);
  assert.match(styles, /html\[data-view="tracker"\] \.panel-shell \{[\s\S]*margin:\s*0/);
});
