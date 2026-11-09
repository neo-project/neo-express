import assert from "node:assert/strict";
import test from "node:test";

import sameFilePath from "./sameFilePath";

test("sameFilePath treats Windows and POSIX separators as equivalent", () => {
  assert.equal(
    sameFilePath(
      "C:/workspace/contracts/Sample/bin/sc/Sample.nef",
      "C:\\workspace\\contracts\\Sample\\bin\\sc\\Sample.nef"
    ),
    true
  );
});

test("sameFilePath keeps unrelated paths distinct", () => {
  assert.equal(
    sameFilePath(
      "C:/workspace/contracts/Sample/bin/sc/Sample.nef",
      "C:/workspace/contracts/Other/bin/sc/Other.nef"
    ),
    false
  );
});
