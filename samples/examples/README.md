# Official contract templates for Neo Express

These folders are the official
[Neo.SmartContract.Template](https://github.com/neo-project/neo-devpack-dotnet/tree/master-n3/src/Neo.SmartContract.Template)
starters, laid out for Neo Express and `Neo.BuildTasks`. Official unit-test projects are not
included.

Walkthrough: [docs/getting-started.md](../../docs/getting-started.md).

## Templates

| Folder | `dotnet new` short name | Contract |
| ------ | ----------------------- | -------- |
| `Blank` | *(copy the folder; no matching template short name)* | Owner + `MyMethod` |
| `Nep17` | `neocontractnep17` | NEP-17 token |
| `Nep11` | `neocontractnep11` | NEP-11 NFT |
| `Oracle` | `neocontractoracle` | Oracle request/response |
| `Ownable` | `neocontractowner` | Owner + `Destroy` |

## Layout

```
*.csproj              # `dotnet build` here compiles and deploys
src/*.cs
express.batch         # optional offline reset+deploy (`neoxp batch`)
default.neo-express   # created on first `dotnet build` if missing
```

Shared `Directory.Build.props` sets `Neo.SmartContract.Framework` and `Neo.BuildTasks`,
builds the repository `neoxp` when available, and deploys the compiled contract to the
example's `default.neo-express` file on first build.

Local tools (`neoxp`, `nccs`) come from [`samples/.config/dotnet-tools.json`](../.config/dotnet-tools.json).

## Source of the C# starters

The C# source and template names are maintained in
[neo-devpack-dotnet `master-n3`](https://github.com/neo-project/neo-devpack-dotnet/tree/master-n3/src/Neo.SmartContract.Template).
These folders are Express integration copies: they add the project, local-chain, and deploy
files needed by Neo Express while mirroring the canonical starter source. When a starter changes,
sync the corresponding DevPack template and this integration copy together.

## Build, deploy, invoke

From an example folder:

```shell
cd samples/examples/Nep17
dotnet build
```

Or from the repo:

```shell
dotnet build samples/examples/Nep17
dotnet build samples/examples/examples.sln
```

The first build creates `default.neo-express` if needed, compiles `bin/sc/*.nef`, and
deploys with `genesis`. If Neo Express is already running (VS Code **Start Neo Express**),
the build still deploys (`contract deploy --force`). It does not require stopping the chain.

Then:

```shell
neoxp run -i default.neo-express --seconds-per-block 1
neoxp contract run -i default.neo-express Nep17Contract symbol --results
```

`dotnet clean` or `dotnet build -t:Rebuild` deletes the deploy stamp so the next build deploys again.

## VS Code

Open the example folder (or the repo) with Neo N3 Visual DevTracker. **New contract**
offers the same C# starters (plus the existing storage example). After a build, start the
chain from **Blockchains** and invoke from **Smart contracts**.
