<!-- markdownlint-enable -->
# Neo Express quickstart

A longer CLI walkthrough: install, create a chain, compile a C# contract, deploy, and invoke.
For the shortest path (including VS Code), start at [getting-started.md](getting-started.md).

Works on Windows, macOS, and Ubuntu.

## 1. Install Neo Express

### .NET tool (recommended)

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```shell
# From this repository (includes the deploy fee pad; NuGet 3.10.1 does not)
dotnet build src/neoxp/neoxp.csproj

# Bash (run from the repository root)
REPO_ROOT="$(pwd)"
neoxp() { dotnet exec "$REPO_ROOT/src/neoxp/bin/Debug/net10.0/neoxp.dll" "$@"; }

# PowerShell (run from the repository root)
$repoRoot = (Get-Location).Path
function neoxp { dotnet exec "$repoRoot\src\neoxp\bin\Debug\net10.0\neoxp.dll" @args }
```

A published tool (`dotnet tool install Neo.Express -g`) is fine for commands other than
deploy until a release includes the fee pad.

### Release package

1. Download the latest build from [neo-express releases](https://github.com/neo-project/neo-express/releases/latest).
2. Unzip it.
3. Run `neoxp` (`neoxp.exe` on Windows) from that directory.

Platform libraries (RocksDB) are listed in [installation.md](installation.md).

## 2. Create and use a private chain

Use **one** chain file. From the repository, stay in `samples/examples/Nep17`:

```shell
cd samples/examples/Nep17
neoxp create -o default.neo-express
neoxp wallet list -i default.neo-express
neoxp show balances genesis -i default.neo-express
neoxp transfer 1 gas genesis node1 -i default.neo-express
neoxp run -i default.neo-express --seconds-per-block 1
```

`genesis` is the consensus multi-sig that holds the genesis NEO and GAS. `node1` is the
default consensus-node wallet.

Leave `neoxp run` going. Other commands use a second terminal in the same folder.

Full command list: [command-reference.md](command-reference.md).

## 3. Compile a C# contract

This repo already has Express-ready starters. Stay in `samples/examples/Nep17`:

```shell
dotnet build
```

That:

- restores local `neoxp` and `nccs` tools
- compiles `Nep17Contract.nef` / `.manifest.json` to `samples/examples/Nep17/bin/sc`
- creates `default.neo-express` next to the example if it is missing
- resets the chain and deploys with `genesis` (`express.batch`)

| Starter | Path |
| ------- | ---- |
| Blank | `samples/examples/Blank` |
| NEP-17 token | `samples/examples/Nep17` |
| NEP-11 NFT | `samples/examples/Nep11` |
| Oracle | `samples/examples/Oracle` |
| Ownable | `samples/examples/Ownable` |

Details: [samples/examples/README.md](../samples/examples/README.md).

### Create your own contract

**VS Code:** Quick Start / Smart contracts → **New contract** → C# → pick Blank, NEP-17,
NEP-11, Oracle, Ownable, or Storage.

**Terminal:** install [Neo.SmartContract.Template](https://www.nuget.org/packages/Neo.SmartContract.Template)
and add `Neo.BuildTasks` like the examples, or copy an example folder:

```shell
dotnet new install Neo.SmartContract.Template
dotnet new neocontractnep17 -n MyToken -o ./MyToken
```

`dotnet build` on an example project runs `nccs` through `Neo.BuildTasks`. You can also run
`nccs` yourself after `dotnet tool install Neo.Compiler.CSharp -g`. Output is `bin/sc/*.nef`.

## 4. Deploy and invoke

If you used `samples/examples/*`, the first `dotnet build` already deployed. With `neoxp run`
in another terminal:

```shell
neoxp contract run -i default.neo-express Nep17Contract symbol --results
```

`--results` is a dry run. To submit a transaction:

```shell
neoxp contract run -i default.neo-express Nep17Contract symbol --account genesis
```

Deploy a `.nef` you compiled yourself (from this folder):

```shell
neoxp contract deploy ./bin/sc/Nep17Contract.nef genesis -i default.neo-express
```

Reusable calls belong in a `.neo-invoke.json` file:

```shell
neoxp contract invoke ./invoke-files/symbol.neo-invoke.json genesis -i default.neo-express
```

See [Neo Express Invocation File](Neo%20Express%20Invocation%20File.md).

## 5. Rebuild so the contract redeploys

`Neo.BuildTasks` skips the Express batch when the `.nef` has not changed. After **Clean**
or **Rebuild**, the stamp is deleted and the next build resets the chain and deploys again:

```shell
dotnet build -t:Rebuild
```

Checkpoint-backed `dotnet test` workflow: [contract-testing.md](contract-testing.md).
