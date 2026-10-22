import assert from "node:assert/strict";
import test from "node:test";

import { formatCli, redactCliArgs } from "./redactCli";

test("redactCliArgs masks --password values used in the options array", () => {
  assert.deepEqual(
    redactCliArgs(["wallet", "delete", "--password", "s3cret", "alice"]),
    ["wallet", "delete", "--password", "********", "alice"]
  );
  assert.deepEqual(redactCliArgs(["-p", "s3cret"]), ["-p", "********"]);
  assert.deepEqual(
    redactCliArgs(["--password=s3cret"]),
    ["--password=********"]
  );
});

test("formatCli never includes the raw password in a log line", () => {
  const line = formatCli("neoxp", [
    "contract",
    "deploy",
    "Contract.nef",
    "alice",
    "--password",
    "hunter2",
  ]);
  assert.match(line, /--password \*{8}/);
  assert.doesNotMatch(line, /hunter2/);
});
