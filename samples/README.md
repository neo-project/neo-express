# Samples

Two layouts you can copy:

| Path | What it shows |
| ---- | ------------- |
| [`src/`](src) + [`test/`](test) | Simple `SampleContract` (`TokenContract`) with `Neo.BuildTasks`, an Express batch, a checkpoint, and `dotnet test` |
| [`examples/`](examples/README.md) | Official [Neo.SmartContract.Template](https://github.com/neo-project/neo-devpack-dotnet/tree/master-n3/src/Neo.SmartContract.Template) starters (Blank, NEP-17, NEP-11, Oracle, Ownable) wired for Express — no official unit-test projects |

## Get started

```shell
dotnet build samples/examples/Blank
dotnet test samples/test
```

The first example build restores local tools, creates `default.neo-express` if needed,
compiles the `.nef`, resets the chain, and deploys with `genesis`. Then start the chain
(global `neoxp`, or `cd samples` and `dotnet tool restore` first):

```shell
neoxp run -i samples/examples/Blank/default.neo-express --seconds-per-block 1
```

In another terminal:

```shell
neoxp contract run -i samples/examples/Blank/default.neo-express Contract MyMethod --results
```

Walkthrough: [docs/getting-started.md](../docs/getting-started.md).
Checkpoint tests: [docs/contract-testing.md](../docs/contract-testing.md).
