# Neo-Express Installation

Neo-Express ships as a set of cross-platform [.NET global tools](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools):

| Package | Command | Purpose |
| ------- | ------- | ------- |
| [`Neo.Express`](https://www.nuget.org/packages/Neo.Express/) | `neoxp` | Manage a local Neo N3 private network: nodes, wallets, contracts, and batch automation. |
| [`Neo.Trace`](https://www.nuget.org/packages/Neo.Trace/) | `neotrace` | Generate trace files for the Neo Smart Contract Debugger from public-chain blocks/transactions. |
| [`Neo.WorkNet`](https://www.nuget.org/packages/Neo.WorkNet/) | `neo-worknet` | Branch a public MainNet/TestNet chain into a local single-node development chain. |

## Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) or later.

## Install via .NET tool (recommended)

Install the tools globally:

```shell
dotnet tool install Neo.Express -g
dotnet tool install Neo.Trace -g
dotnet tool install Neo.WorkNet -g
```

Update them to the latest version with `dotnet tool update`:

```shell
dotnet tool update Neo.Express -g
dotnet tool update Neo.Trace -g
dotnet tool update Neo.WorkNet -g
```

Confirm the installation:

```shell
neoxp --version
```

## Install via release package

If you prefer a self-contained build that does not require the .NET SDK, download a
release package instead:

1. Download the latest package for your operating system from the
   [neo-express releases](https://github.com/neo-project/neo-express/releases/latest) page.
2. Unzip the package.
3. Run `neoxp` (`neoxp.exe` on Windows) from the unzipped directory.

| Platform | Download |
| -------- | -------- |
| Windows  | [Latest Windows release](https://github.com/neo-project/neo-express/releases/latest) |
| macOS    | [Latest macOS release](https://github.com/neo-project/neo-express/releases/latest) |
| Linux    | [Latest Linux release](https://github.com/neo-project/neo-express/releases/latest) |

## Platform-specific requirements

Neo-Express uses RocksDB for storage, which requires a few native libraries on some platforms.

### Ubuntu

> **Note:** Installing .NET via Snap is [known to cause a segmentation fault](https://github.com/dotnet/runtime/issues/3775#issuecomment-534263315)
> in Neo-Express, and that [issue will not be fixed](https://github.com/dotnet/runtime/issues/3775#issuecomment-888676286).
> Install .NET [using APT](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu) instead.

Install the required native libraries:

```shell
sudo apt install libsnappy-dev libc6-dev librocksdb-dev -y
```

### macOS

Install RocksDB via [Homebrew](https://brew.sh/):

```shell
brew install rocksdb
```

Apple Silicon is supported by both .NET and Homebrew. If you run into problems on Apple
Silicon hardware, please [open an issue](https://github.com/neo-project/neo-express/issues).

## Next steps

- The [readme](../readme.md) has a quick-start guide and a command overview.
- [settings.md](settings.md) documents the `.neo-express` configuration values.
