# Official C# contract templates

Contract source is not copied into this repository. Install
[Neo.SmartContract.Template](https://www.nuget.org/packages/Neo.SmartContract.Template)
and create a project with `dotnet new`:

```shell
dotnet new install Neo.SmartContract.Template
dotnet new list
```

| `dotnet new` short name | What it creates |
| ----------------------- | --------------- |
| `neocontract` | Solution: contract + unit-test project |
| `neocontractnep17` | NEP-17 token |
| `neocontractnep11` | NEP-11 NFT |
| `neocontractoracle` | Oracle request/response |
| `neocontractowner` | Owner + `Destroy` |

```shell
dotnet new neocontractnep17 -n Nep17Contract -o Nep17
cd Nep17
dotnet build
```

The compiler writes `bin/sc/*.nef`. Create a chain and deploy with Neo Express:

```shell
neoxp create -o default.neo-express
neoxp run --seconds-per-block 1
```

In another terminal:

```shell
neoxp contract deploy bin/sc/Nep17Contract.nef genesis
neoxp contract run Nep17Contract symbol --results
```

The VS Code **New contract** wizard installs the same package and runs
`dotnet new` for those official C# starters, then adds Express test projects.
The storage starter stays an Express-only scaffold.

Walkthrough: [docs/quickstart.md](../../docs/quickstart.md).
