<!-- markdownlint-enable -->
# Testing Neo Smart Contracts in C#

This repository ships four NuGet packages that together provide an end-to-end workflow for
building and testing Neo N3 smart contracts from a standard `dotnet test` run — no running
network required:

| Package | Purpose |
| ------- | ------- |
| `Neo.BuildTasks` | MSBuild tasks that compile the contract, generate a typed interface for it, and run a Neo-Express batch file as part of the build |
| `Neo.Test.Harness` | Checkpoint-backed xUnit fixtures and a `TestApplicationEngine` for executing contract code in-process |
| `Neo.Assertions` | [FluentAssertions](https://fluentassertions.com/) extensions for `StackItem`, `NotifyEventArgs` and `StorageItem` |
| `Neo.Collector` | A VSTest data collector that reports per-instruction contract coverage in Cobertura and LCOV formats |

A complete working example lives under [`samples/`](../samples): a contract project, a test
project, and the batch file that connects them.

## 1. The contract project

Add `Neo.BuildTasks` to the contract project and set `NeoContractName`. The build then invokes
the [`nccs`](https://github.com/neo-project/neo-devpack-dotnet) compiler after the normal C#
compile and drops `<name>.nef`, `<name>.manifest.json` and `<name>.nefdbgnfo` under `bin/sc`
([`samples/src/contract.csproj`](../samples/src/contract.csproj)):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <NeoContractName>$(AssemblyName)</NeoContractName>
    <NeoExpressBatchFile>../express.batch</NeoExpressBatchFile>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Neo.SmartContract.Framework" Version="3.10.1" />
    <PackageReference Include="Neo.BuildTasks" Version="3.10.1" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Useful MSBuild properties understood by `Neo.BuildTasks`:

| Property | Default | Effect |
| -------- | ------- | ------ |
| `NeoContractName` | *(required)* | Base name of the compiled `.nef`/manifest/debug-info files; enables contract compilation |
| `NeoContractOutput` | `bin/sc` | Output folder for the compiled contract |
| `NeoCscDebugInfo` | `true` | Emit `.nefdbgnfo` debug information (needed for coverage) |
| `NeoContractAssembly` | `false` | Also emit a `.asm` disassembly listing |
| `NeoExpressBatchFile` | *(unset)* | Run the given Neo-Express batch file after the contract builds |
| `NeoExpressBatchInputFile` | *(unset)* | `.neo-express` file the batch runs against |
| `NeoExpressBatchNoReset` | `false` | Skip the chain reset that normally precedes the batch |

## 2. Creating the test checkpoint

Contract tests run against a **checkpoint**: a frozen snapshot of a Neo-Express chain with the
contract already deployed. The sample produces one automatically on every contract build via
`NeoExpressBatchFile` and this two-line batch file
([`samples/express.batch`](../samples/express.batch)):

```
contract deploy ./src/bin/sc/contract.nef genesis
checkpoint create ./checkpoints/contract-deployed -f
```

The batch runs against the default `.neo-express` file, resetting the chain first, so the
checkpoint is reproducible: genesis state, plus your contract, and nothing else. Any other
setup the tests need (funded accounts, initial contract invocations, minted tokens) can be
added as additional batch lines. See the
[batch command documentation](command-reference.md#neoxp-batch) for the supported commands.

## 3. The test project

The test project references the packages, plus a `NeoContractReference` to the contract
project ([`samples/test/contract-test.csproj`](../samples/test/contract-test.csproj)):

```xml
<ItemGroup>
  <NeoContractReference Include="..\src\contract.csproj" />
</ItemGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.3.0" />
  <PackageReference Include="Neo.Assertions" Version="3.10.1" />
  <PackageReference Include="Neo.BuildTasks" Version="3.10.1" PrivateAssets="all" />
  <PackageReference Include="Neo.Test.Harness" Version="3.10.1" />
  <PackageReference Include="xunit" Version="2.4.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
</ItemGroup>
```

`NeoContractReference` builds the contract before the tests and generates a C# interface from
the contract manifest — one method per contract method, named after the contract. The tests
use this interface to build invocation scripts with compile-time checking instead of
hand-written operation strings.

## 4. Writing tests

Tests bind a checkpoint to an xUnit class fixture with the `CheckpointPath` attribute
([`samples/test/contract-tests.cs`](../samples/test/contract-tests.cs)):

```csharp
[CheckpointPath("checkpoints/contract-deployed.neoxp-checkpoint")]
public class ContractDeployedTests : IClassFixture<CheckpointFixture<ContractDeployedTests>>
{
    readonly CheckpointFixture fixture;
    readonly ExpressChain chain;

    public ContractDeployedTests(CheckpointFixture<ContractDeployedTests> fixture)
    {
        this.fixture = fixture;
        this.chain = fixture.FindChain();
    }

    [Fact]
    public void TestSymbol()
    {
        var settings = chain.GetProtocolSettings();
        using var snapshot = fixture.GetSnapshot();
        using var engine = new TestApplicationEngine(snapshot, settings);

        var state = engine.ExecuteScript<contract>(c => c.symbol());

        engine.State.Should().Be(VMState.HALT);
        engine.ResultStack.Should().HaveCount(1);
        engine.ResultStack.Peek(0).Should().BeEquivalentTo("TEST");
    }
}
```

The relative checkpoint path is resolved by walking up from the test's working directory, so
it works both from the IDE and from `dotnet test`. Each test takes its own snapshot of the
checkpoint — tests are isolated and can run in any order. `TestApplicationEngine` executes
the script in-process against that snapshot: no node, no block times, millisecond execution.

Beyond executing scripts, `Neo.Test.Harness` and `Neo.Assertions` provide:

- `engine.GetContractStorages<contract>()` — the contract's storage as a dictionary, with
  `StorageMap(prefix)` and `TryGetValue(key, out item)` helpers for drilling into storage maps;
- signer-aware engines — `new TestApplicationEngine(snapshot, settings, signer, witnessScope)`
  runs the script as a specific account so `CheckWitness`-guarded paths can be tested;
- `.Should()` assertions on `StackItem` (`BeEquivalentTo` against strings, integers, booleans,
  `UInt160`/`UInt256`, plus `BeTrue`/`BeFalse`), on `NotifyEventArgs` (`BeEquivalentTo` against a
  typed event expression, `BeSentBy` a contract) and on `StorageItem`.

## 5. Contract code coverage

`Neo.Collector` plugs into VSTest as a data collector and records which contract instructions
each test executed. Add the package to the test project, then enable the collector with a
`.runsettings` file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="Neo code coverage">
        <Configuration>
          <DebugInfo name="contract">../src/bin/sc/contract.nefdbgnfo</DebugInfo>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

and run:

```shell
dotnet test --settings coverage.runsettings
```

Each `DebugInfo` element points at a compiled contract's `.nefdbgnfo` (or `.debug.json`) so
the collector can map executed instructions back to source lines. The collector attaches two
reports to the test run: `neo-coverage.cobertura.xml` (Cobertura, consumed by most CI coverage
services) and `neo-coverage.lcov.info` (LCOV, consumed by editor coverage gutters and
`genhtml`). An optional `<VerboseLog>true</VerboseLog>` element inside `Configuration` turns
on collector diagnostics.
