import assert from "node:assert/strict";
import * as http from "node:http";
import { createRequire } from "node:module";
import test from "node:test";

type ModuleWithLoad = typeof import("node:module") & {
  _load: (
    request: string,
    parent: NodeJS.Module | null,
    isMain: boolean
  ) => unknown;
};

const requireFromTest = createRequire(__filename);
const moduleLoader = requireFromTest("node:module") as ModuleWithLoad;
const originalLoad = moduleLoader._load;

moduleLoader._load = function (
  request: string,
  parent: NodeJS.Module | null,
  isMain: boolean
) {
  if (request === "vscode") {
    return {
      window: {
        createOutputChannel: () => ({
          appendLine: () => undefined,
          dispose: () => undefined,
        }),
      },
    };
  }

  return originalLoad.call(this, request, parent, isMain);
};

const tryFetchJson = requireFromTest("./tryFetchJson")
  .default as typeof import("./tryFetchJson").default;
moduleLoader._load = originalLoad;

type TestServer = {
  close: () => Promise<void>;
  host: string;
};

async function createTestServer(
  handler: http.RequestListener
): Promise<TestServer> {
  const server = http.createServer(handler);
  await new Promise<void>((resolve) => {
    server.listen(0, "127.0.0.1", resolve);
  });

  const address = server.address();
  assert.ok(address && typeof address === "object");

  return {
    close: () =>
      new Promise<void>((resolve, reject) => {
        server.close((err) => {
          if (err) {
            reject(err);
          } else {
            resolve();
          }
        });
      }),
    host: `127.0.0.1:${address.port}`,
  };
}

test("tryFetchJson returns parsed JSONC from successful responses", async () => {
  const server = await createTestServer((request, response) => {
    assert.equal(request.url, "/templates/neo");
    response.end('{ "name": "neo", // comment\n "enabled": true }');
  });

  try {
    assert.deepEqual(await tryFetchJson("http", server.host, "/templates/neo"), {
      enabled: true,
      name: "neo",
    });
  } finally {
    await server.close();
  }
});

test("tryFetchJson returns an empty object for non-200 responses", async () => {
  const server = await createTestServer((_request, response) => {
    response.statusCode = 404;
    response.end("missing");
  });

  try {
    assert.deepEqual(await tryFetchJson("http", server.host, "/missing"), {});
  } finally {
    await server.close();
  }
});

test("tryFetchJson returns an empty object for malformed JSONC", async () => {
  const server = await createTestServer((_request, response) => {
    response.end("{{{{");
  });

  try {
    assert.deepEqual(await tryFetchJson("http", server.host, "/bad"), {});
  } finally {
    await server.close();
  }
});
