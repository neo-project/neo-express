import assert from "node:assert/strict";
import test from "node:test";

import { parseViewRequest } from "./viewRequest";

test("parseViewRequest rejects non-object envelopes", () => {
  assert.equal(parseViewRequest(undefined), undefined);
  assert.equal(parseViewRequest(null), undefined);
  assert.equal(parseViewRequest("typed-request"), undefined);
  assert.equal(parseViewRequest([]), undefined);
});

test("parseViewRequest rejects non-object typed requests", () => {
  assert.equal(parseViewRequest({ typedRequest: "open" }), undefined);
  assert.equal(parseViewRequest({ typedRequest: 1 }), undefined);
  assert.equal(parseViewRequest({ typedRequest: null }), undefined);
  assert.equal(parseViewRequest({ typedRequest: [] }), undefined);
});

test("parseViewRequest accepts supported request shapes", () => {
  assert.deepEqual(parseViewRequest({ retrieveViewState: true }), {
    retrieveViewState: true,
  });
  assert.deepEqual(parseViewRequest({ typedRequest: { command: "open" } }), {
    typedRequest: { command: "open" },
  });
});
