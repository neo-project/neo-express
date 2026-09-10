import assert from "node:assert/strict";
import { mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";

import { hydrateFiles, substituteParameters } from "./hydrateTemplates";

const packageRoot = join(__dirname, "../../..");

test("substituteParameters replaces every placeholder", () => {
  assert.equal(
    substituteParameters("src/$_CLASSNAME_$.cs in $_CONTRACTNAME_$", {
      $_CLASSNAME_$: "TokenEscrowContract",
      $_CONTRACTNAME_$: "TokenEscrow",
    }),
    "src/TokenEscrowContract.cs in TokenEscrow"
  );
});

test("hydrateFiles overlays a C# starter onto the scaffold", async () => {
  const destination = await mkdtemp(join(tmpdir(), "neoxp-template-"));
  const parameters = {
    $_CLASSNAME_$: "TokenEscrowContract",
    $_CONTRACTNAME_$: "TokenEscrow",
    $_MAINFILE_$: "src/TokenEscrowContract.cs",
  };

  try {
    await hydrateFiles(
      join(packageRoot, "resources/new-contract/csharp"),
      destination,
      parameters
    );
    await hydrateFiles(
      join(packageRoot, "resources/new-contract/csharp-starters/nep17"),
      destination,
      parameters
    );

    const contract = await readFile(
      join(destination, "src/TokenEscrowContract.cs"),
      "utf8"
    );
    assert.match(contract, /class TokenEscrowContract : Nep17Token/);
    assert.match(contract, /namespace TokenEscrow/);
    assert.doesNotMatch(contract, /\$_CLASSNAME_\$/);
    assert.doesNotMatch(contract, /ChangeNumber/);

    const tests = await readFile(
      join(destination, "test/TokenEscrowContractTests.cs"),
      "utf8"
    );
    assert.match(tests, /c\.symbol\(\)/);
    assert.match(tests, /EXAMPLE/);

    const csproj = await readFile(
      join(destination, "src/TokenEscrowContract.csproj"),
      "utf8"
    );
    assert.match(csproj, /Neo\.BuildTasks/);
    assert.match(csproj, /NeoContractName>TokenEscrowContract/);

    const tools = JSON.parse(
      await readFile(join(destination, ".config/dotnet-tools.json"), "utf8")
    );
    assert.equal(tools.tools["neo.express"].commands[0], "neoxp");
  } finally {
    await rm(destination, { recursive: true, force: true });
  }
});

test("hydrateFiles can create a blank official starter", async () => {
  const destination = await mkdtemp(join(tmpdir(), "neoxp-blank-"));
  const parameters = {
    $_CLASSNAME_$: "HelloContract",
    $_CONTRACTNAME_$: "Hello",
  };

  try {
    await hydrateFiles(
      join(packageRoot, "resources/new-contract/csharp"),
      destination,
      parameters
    );
    await hydrateFiles(
      join(packageRoot, "resources/new-contract/csharp-starters/blank"),
      destination,
      parameters
    );
    const contract = await readFile(
      join(destination, "src/HelloContract.cs"),
      "utf8"
    );
    assert.match(contract, /class HelloContract : SmartContract/);
    assert.match(contract, /MyMethod/);
    assert.doesNotMatch(contract, /ChangeNumber/);
  } finally {
    await rm(destination, { recursive: true, force: true });
  }
});

const overlayStarters: {
  id: string;
  className: string;
  abiCall: RegExp;
}[] = [
  { id: "blank", className: "HelloContract", abiCall: /c\.myMethod\(\)/ },
  { id: "nep17", className: "TokenEscrowContract", abiCall: /c\.symbol\(\)/ },
  { id: "nep11", className: "CollectibleContract", abiCall: /c\.symbol\(\)/ },
  { id: "oracle", className: "FeedContract", abiCall: /c\.getResponse\(\)/ },
  { id: "ownable", className: "VaultContract", abiCall: /c\.myMethod\(\)/ },
];

test("every official C# starter hydrates, uses ABI casing, and builds", async () => {
  const { spawnSync } = await import("node:child_process");
  for (const starter of overlayStarters) {
    const destination = await mkdtemp(join(tmpdir(), `neoxp-${starter.id}-`));
    const parameters = {
      $_CLASSNAME_$: starter.className,
      $_CONTRACTNAME_$: starter.className.replace(/Contract$/, ""),
      $_MAINFILE_$: `src/${starter.className}.cs`,
    };
    try {
      await hydrateFiles(
        join(packageRoot, "resources/new-contract/csharp"),
        destination,
        parameters
      );
      await hydrateFiles(
        join(packageRoot, "resources/new-contract/csharp-starters", starter.id),
        destination,
        parameters
      );

      const tests = await readFile(
        join(destination, `test/${starter.className}Tests.cs`),
        "utf8"
      );
      assert.match(
        tests,
        starter.abiCall,
        `${starter.id} tests must call the ABI (camelCase) member`
      );
      assert.doesNotMatch(tests, /c\.(MyMethod|GetResponse)\(/);

      const testCsproj = await readFile(
        join(destination, `test/${starter.className}Tests.csproj`),
        "utf8"
      );
      assert.match(testCsproj, /NeoExpressBatchInputFile>..\/default\.neo-express/);
      assert.match(testCsproj, /create -o default\.neo-express/);
      assert.doesNotMatch(testCsproj, /Tests\.neo-express/);

      const restore = spawnSync("dotnet", ["tool", "restore"], {
        cwd: destination,
        encoding: "utf8",
      });
      assert.equal(
        restore.status,
        0,
        `${starter.id} tool restore failed:\n${restore.stdout}\n${restore.stderr}`
      );

      const buildSrc = spawnSync(
        "dotnet",
        ["build", join("src", `${starter.className}.csproj`), "-v", "q"],
        { cwd: destination, encoding: "utf8" }
      );
      assert.equal(
        buildSrc.status,
        0,
        `${starter.id} src failed to build:\n${buildSrc.stdout}\n${buildSrc.stderr}`
      );

      const buildTests = spawnSync(
        "dotnet",
        [
          "build",
          join("test", `${starter.className}Tests.csproj`),
          "-v",
          "q",
          "/p:NeoExpressBatchFile=",
        ],
        { cwd: destination, encoding: "utf8" }
      );
      assert.equal(
        buildTests.status,
        0,
        `${starter.id} tests failed to build:\n${buildTests.stdout}\n${buildTests.stderr}`
      );
    } finally {
      await rm(destination, { recursive: true, force: true });
    }
  }
});
