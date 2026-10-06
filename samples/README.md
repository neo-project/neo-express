# Samples

Two layouts:

| Path | What it shows |
| ---- | ------------- |
| [`src/`](src) + [`test/`](test) | Simple `SampleContract` (`TokenContract`) with `Neo.BuildTasks`, an Express batch, a checkpoint, and `dotnet test` |
| [`examples/`](examples/README.md) | Official [Neo.SmartContract.Template](https://www.nuget.org/packages/Neo.SmartContract.Template) via `dotnet new` — no contract source is copied here |

## Get started

Express sample (this repo):

```shell
dotnet test samples/test
```

Official templates:

```shell
dotnet new install Neo.SmartContract.Template
dotnet new neocontractnep17 -n Nep17Contract -o Nep17
cd Nep17
dotnet build
neoxp create -o default.neo-express
neoxp run --seconds-per-block 1
```

In another terminal:

```shell
neoxp contract deploy bin/sc/Nep17Contract.nef genesis
neoxp contract run Nep17Contract symbol --results
```

Walkthrough: [docs/quickstart.md](../docs/quickstart.md).
Checkpoint tests: [docs/contract-testing.md](../docs/contract-testing.md).
