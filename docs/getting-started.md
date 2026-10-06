<!-- markdownlint-enable -->
# Getting started

This is the shortest path from a clean machine to a running local Neo N3 chain with a
contract you can invoke. Use [Visual Studio Code](#use-visual-studio-code) or the
[command line](#use-the-command-line). Both use the same tools: Neo Express (`neoxp`),
the C# compiler (`nccs`), and `Neo.BuildTasks`.

## What you need

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) (`dotnet --list-sdks`
  should list a `10.x` version)
- Optional: [Visual Studio Code](https://code.visualstudio.com/Download) 1.104 or later
- Optional: Node.js 20+ only if you load the Visual DevTracker extension from this repo

On Ubuntu, install RocksDB libraries and **do not** install .NET via Snap (see
[installation](installation.md#ubuntu)). On macOS, `brew install rocksdb`.

## Use the command line

Work in **one folder** for the rest of this page: a contract you create with
`dotnet new neocontractnep17`. That is the only `default.neo-express` these steps use.

Published `Neo.Express` 3.10.1 can print “Transaction submitted” while persist-time `_deploy`
FAULTs with Insufficient GAS, so the next `contract run` fails. Build `neoxp` from **this
repository** (the fee pad lives here until the next NuGet release).

### 1. Build Neo Express from this repository

From the repository root:

```shell
dotnet tool restore --tool-manifest samples/.config/dotnet-tools.json
dotnet build src/neoxp/neoxp.csproj
```

Then either add `src/neoxp/bin/Debug/net10.0` to `PATH`, or define a helper **before**
`cd` so it still points at the repository root:

```shell
# bash (run from the repository root)
REPO_ROOT="$(pwd)"
neoxp() { dotnet exec "$REPO_ROOT/src/neoxp/bin/Debug/net10.0/neoxp.dll" "$@"; }

# PowerShell (run from the repository root)
$repoRoot = (Get-Location).Path
function neoxp { dotnet exec "$repoRoot\src\neoxp\bin\Debug\net10.0\neoxp.dll" @args }
```

`nccs` and the sample `neoxp` tool are pinned in the sample tool manifest; the command above
restores them before you build a sample.
Full install options (release zip, Trace, WorkNet) are in [installation.md](installation.md).

### 2. Create a contract from the official template

From the repository root (or any working folder):

```shell
dotnet new install Neo.SmartContract.Template
dotnet new neocontractnep17 -n Nep17Contract -o Nep17
cd Nep17
dotnet build
```

That compiles `bin/sc/Nep17Contract.nef`. Short names: `neocontract` (solution + tests),
`neocontractnep17`, `neocontractnep11`, `neocontractoracle`, `neocontractowner`.
Details: [`samples/examples/README.md`](../samples/examples/README.md).

The original simple sample (a `TokenContract` plus checkpoint tests) is
[`samples/src`](../samples/src) — see [contract testing](contract-testing.md).

### 3. Create and run a local chain

Still in `Nep17`:

```shell
neoxp create -o default.neo-express
neoxp wallet list -i default.neo-express
neoxp run -i default.neo-express --seconds-per-block 1
```

`neoxp create` writes `default.neo-express` in this folder (genesis plus `node1`).
Leave `neoxp run` in that terminal. In another terminal in the **same folder**:

```shell
neoxp show balances genesis -i default.neo-express
```

By default a new block is minted every 15 seconds. `--seconds-per-block 1` makes transfers
and deploys show up immediately while you are iterating.

### 4. Deploy and invoke the contract

With the chain still running, from `Nep17`:

```shell
neoxp contract deploy bin/sc/Nep17Contract.nef genesis -i default.neo-express
neoxp contract run -i default.neo-express Nep17Contract symbol --results
neoxp contract run -i default.neo-express Nep17Contract decimals --results
```

`--results` is a trial run (no transaction). Drop it and pass `--account genesis` to submit.

Reusable calls belong in a `.neo-invoke.json` file:

```json
[{ "contract": "Nep17Contract", "operation": "symbol" }]
```

```shell
neoxp contract invoke ./symbol.neo-invoke.json genesis -i default.neo-express
```

File format: [Neo Express Invocation File](Neo%20Express%20Invocation%20File.md).

In VS Code, select the rocket on a workspace contract to open Contract Studio, pick a
method, choose the signing account, and run.

### 5. Reset and rebuild

From `Nep17`:

```shell
# stop the node before resetting its persisted state
neoxp stop -a -i default.neo-express

# wipe chain state (keeps wallets)
neoxp reset -f -i default.neo-express

dotnet build
neoxp contract deploy bin/sc/Nep17Contract.nef genesis -i default.neo-express --force

# start the chain again for subsequent invocations
neoxp run -i default.neo-express --seconds-per-block 1
```

## New contract wizard in Visual Studio Code

1. Open a folder in VS Code.
2. Open the Neo N3 Visual DevTracker view (Neo logo in the activity bar).
3. **Quick Start** or **Smart contracts** → **New contract**.
4. Choose **C#**, then a template: Blank, NEP-17, NEP-11, Oracle, Ownable, or Storage.
5. Name the contract. Files land under `contracts/<name>/`.
6. The scaffold restores tools and builds. Start the Express instance from **Blockchains**,
   connect, then deploy from **Smart contracts**.

How to load the extension from this repo is in
[Use Visual Studio Code](#use-visual-studio-code).

## `dotnet new` from Neo.SmartContract.Template

```shell
dotnet new install Neo.SmartContract.Template
dotnet new neocontractnep17 -n MyToken -o ./MyToken
```

That template compiles with `nccs`. The VS Code **New contract** wizard runs the same
`dotnet new` short names and then adds Express test overlays.

## Use Visual Studio Code

### Load Visual DevTracker from this repo

```shell
cd extentions/neo3-visual-tracker
npm install
npm run compile
```

Then press **F5** in that folder (Extension Development Host), or:

```shell
code --extensionDevelopmentPath="<repo>/extentions/neo3-visual-tracker" "<your-workspace>"
```

The packaged extension bundles `neoxp`. A source checkout without `deps/nxp` uses the
repo build of `src/neoxp` when present, otherwise `neoxp` from PATH.

Open **Quick Start** and work through: create/start Express → New contract → deploy → invoke.

### Debugger

Install the debugger tool and the [Neo Smart Contract Debugger](../extensions/neodebug-vscode/README.md)
extension:

```shell
dotnet tool install Neo.Debug -g
```

Launch configurations are documented in [debugger-command-reference.md](debugger-command-reference.md).
This build of `neodebug` supports both recorded trace replay (`invocation.trace-file`) and live
in-process launches with an `operation` and optional `args`.

## What to read next

| Doc | When |
| --- | ---- |
| [installation.md](installation.md) | Global tools, release zips, Ubuntu/macOS |
| [quickstart.md](quickstart.md) | Longer CLI walkthrough (create, compile, deploy, invoke) |
| [contract-testing.md](contract-testing.md) | `dotnet test` against a checkpoint |
| [samples/examples/README.md](../samples/examples/README.md) | Official C# starters for Express |
| [command-reference.md](command-reference.md) | Every `neoxp` command |
| [settings.md](settings.md) | `.neo-express` settings (block time, AutoMine, …) |
| [Visual DevTracker README](../extentions/neo3-visual-tracker/README.md) | VS Code UI |
| [worknet-command-reference.md](worknet-command-reference.md) | Branch MainNet/TestNet locally |
| [trace-command-reference.md](trace-command-reference.md) | Trace public-chain transactions |
