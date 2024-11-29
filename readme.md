<!-- markdownlint-enable -->
# Neo-Express and Neo-Trace

[![Nuget](https://img.shields.io/nuget/v/Neo.Express)](https://www.nuget.org/packages/Neo.Express/)
[![Build Status](https://dev.azure.com/ngdenterprise/Build/_apis/build/status/neo-project.neo-express?branchName=master)](https://dev.azure.com/ngdenterprise/Build/_build/latest?definitionId=2&branchName=master)

[Neo-Express and Neo-Trace](#neo-express-and-neo-trace)

- [Overview](#overview)
- [Download Links](#download-links)
- [Installation Guide](#installation-guide)
- [Usage Guide](#usage-guide)
- [New Features or issues](#new-features-or-issues)
- [License](#license)

## Overview

Neo-Express is a private net optimized for development scenarios, built on the same Neo platform core as [neo-cli](https://docs.neo.org/docs/en-us/node/cli/setup.html) and [neo-gui](https://docs.neo.org/docs/en-us/node/gui/install.html) to maximize compatibility between local development and public chain environments. Neo-Trace is a tool for generating trace files for the Neo Smart Contract Debugger.

- ### Key Features

  **Neo-Express**:

  - Blockchain instance management
  - Wallet management
  - Asset management
  - Smart contract management
  - Blockchain checkpoint and rollback

  **Neo-Trace**:

  - Generate trace files for Neo Smart Contract Debugger
  - Support specifying blocks by index or hash and transactions by hash

## Download Links

| Platform | Download Link                                                |
| -------- | ------------------------------------------------------------ |
| Windows  | [win-x64-3.7.6.zip](https://github.com/neo-project/neo-express/releases/download/3.7.6/Neo.Express-win-x64-3.7.6.zip) <br/>[win-arm64-3.7.6.zip](https://github.com/neo-project/neo-express/releases/download/3.7.6/Neo.Express-win-arm64-3.7.6.zip) |
| macOS    | [osx-x64-3.7.6.tar.xz](https://github.com/neo-project/neo-express/releases/download/3.7.6/Neo.Express-osx-x64-3.7.6.tar.xz) <br/>[osx-arm64-3.7.6.tar.xz](https://github.com/neo-project/neo-express/releases/download/3.7.6/Neo.Express-osx-arm64-3.7.6.tar.xz) |
| Linux    | [linux-x64-3.7.6.tar.gz](https://github.com/neo-project/neo-express/releases/download/3.7.6/Neo.Express-linux-x64-3.7.6.tar.gz) <br/>[linux-musl-arm64-3.7.6.tar.gz](https://github.com/neo-project/neo-express/releases/download/3.7.6/Neo.Express-linux-musl-arm64-3.7.6.tar.gz) <br/>[linux-arm64-3.7.6.tar.gz](https://github.com/neo-project/neo-express/releases/download/3.7.6/Neo.Express-linux-arm64-3.7.6.tar.gz) |

## Installation Guide

### Install via Release Package

1. Download the latest release package from [neo-express releases](https://github.com/neo-project/neo-express/releases) for your operating system.
2. Unzip the package on your local machine.
3. Run the `neoxp.exe` command in the terminal from the directory where you unzipped the package.

### Install via .NET Tool

#### Requirements

- [.NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) or higher

#### Installation Steps

To install the latest version of Neo-Express as a global tool, run the following command in a terminal window:

```shell
dotnet tool install Neo.Express -g
```

To update Neo-Express to the latest version, run the following command:

```shell
dotnet tool update Neo.Express -g
```

The installation and update process for Neo-Trace is identical:

```shell
dotnet tool install Neo.Trace -g
dotnet tool update Neo.Trace -g
```

### Additional Neo-Express Requirements

#### Ubuntu Installation

> **Note**: While Microsoft has instructions for [installing .NET via Snap](https://docs.microsoft.com/en-us/dotnet/core/install/linux-snap), there is a [known issue](https://github.com/dotnet/runtime/issues/3775#issuecomment-534263315) with this approach that leads to a segmentation fault in Neo Express. Unfortunately, this issue with the .NET snap installer [has been closed and will not be fixed](https://github.com/dotnet/runtime/issues/3775#issuecomment-888676286). As such, we recommend [using APT](https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu) to install .NET on Ubuntu instead.

Installing on Ubuntu requires installing `libsnappy-dev`, `libc6-dev`, and `librocksdb-dev` via apt-get:

```shell
sudo apt install libsnappy-dev libc6-dev librocksdb-dev -y
```

#### macOS Installation

Installing on macOS requires installing rocksdb via [Homebrew](https://brew.sh/):

```shell
brew install rocksdb
```

> **Note**: .NET 6 Arm64 has [full support for Apple Silicon](https://devblogs.microsoft.com/dotnet/announcing-net-6/#arm64). Homebrew likewise also supports Apple Silicon. If you have any issues running Neo-Express on Apple Silicon hardware, please [open an issue](https://github.com/neo-project/neo-express/issues) in the Neo-Express repo.

## Usage Guide

### Neo-Express

- Create a new local Neo network:

  ```shell
  neoxp create
  ```

- List all wallets:

  ```shell
  neoxp wallet list
  ```

- Show genesis account balance:

  `genesis` to use the consensus node multi-sig account which holds the genesis NEO and GAS.
  
  ```shell
  neoxp show balances genesis
  ```

- Send 1 gas from genesis account to node1 account:

  ```shell
  neoxp transfer 1 gas genesis node1
  ```

Please review the [Command Reference](docs/command-reference.md) to get an understanding of Neo-Express capabilities.

### Neo-Trace

- Generate a trace file for a block:

  ```shell
  neotrace block 365110 --rpc-uri testnet
  ```

- Generate a trace file for a transaction:
  
  ```shell
  neotrace tx 0xef1917b8601828e1d2f3ed0954907ea611cb734771609ce0ce2b654bb5c78005 --rpc-uri testnet
  ```

> Note: Neo-Trace depends on the [StateService plugin module](https://github.com/neo-project/neo-modules/tree/master/src/StateService) running with `FullState` enabled. The official JSON-RPC nodes for MainNet and TestNet (such as `http://seed1.neo.org:10332` and `http://seed1t5.neo.org:20332`) are configured to run the StateService plugin with `FullState` enabled.

## New Features or issues

Thank you for using Neo-Express and Neo-Trace! We welcome your feedback to make these tools more accessible, intuitive, and powerful.

Please visit the [issues page](https://github.com/neo-project/neo-express/issues) to report problems or suggest new features. When creating a new issue, try to keep the title and description concise and provide context, such as a code snippet or an example of a feature and its expected behavior.

## License

Neo-Express and Neo-Trace are licensed under the [MIT License](https://github.com/neo-project/neo-express#MIT-1-ov-file).
