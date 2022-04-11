<!-- markdownlint-enable -->
# NeoExpress and NeoTrace

[![Nuget](https://img.shields.io/nuget/v/Neo.Express)](https://www.nuget.org/packages/Neo.Express/)
[![Build Status](https://dev.azure.com/ngdenterprise/Build/_apis/build/status/neo-project.neo-express?branchName=master)](https://dev.azure.com/ngdenterprise/Build/_build/latest?definitionId=2&branchName=master)

Neo-Express is a Private Net that is optimized for
development scenarios. It is built on the same Neo platform core as
[neo-cli](https://docs.neo.org/docs/en-us/node/cli/setup.html) and
[neo-gui](https://docs.neo.org/docs/en-us/node/gui/install.html) ensuring that
blockchain application code runs the same in production as it does in the
Neo-Express development environment.

Neo-Express provides the following features:

- Blockchain instance management
- Wallet management
- Asset management
- Smart contract management
- Blockchain checkpoint and rollback

Docs are somewhat limited at this point. Please review the
[Command Reference](docs/command-reference.md) to get an understanding of
Neo-Express capabilities.

> Note, Neo-Express has been updated to use a similar branch structure as other repos in the 
> Neo project. The `master` branch is for the Neo N3 compatible version of Neo-Express
> and the `master-2.x` branch is for the Neo Legacy compatible version.

## Installation

Neo-Express is distributed as a
[.NET Core Tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools).
Different versions of Neo-Express require different versions of .NET Core.

|Neo-Express Version|.NET Core Version|
|-------------------|-----------------|
| v3.1 | [v6.0](https://dotnet.microsoft.com/download/dotnet/6.0) |
| v3.0 | [v5.0](https://dotnet.microsoft.com/download/dotnet/5.0) |
| v1.1 | [v3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) |
| v1.0 | [v3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) |
| v0.9 | [v3.0](https://dotnet.microsoft.com/download/dotnet-core/3.0) |
| v0.8 | [v2.2](https://dotnet.microsoft.com/download/dotnet-core/2.2) |

To install Neo-Express, open a terminal window and enter the following command:

``` shell
> dotnet tool install Neo.Express -g
```

To upgrade Neo-Express, enter the following command in a terminal window:

``` shell
> dotnet tool update Neo.Express -g
```

### Ubuntu Installation

> Note, while .NET 6 can be [installed with Snap](https://docs.microsoft.com/en-us/dotnet/core/install/linux-snap), there
> appears to be an issue leading to a [segmentation fault](https://github.com/dotnet/runtime/issues/67465) when .NET is 
> installed this way. At this time, we recommend [using APT](https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu)
> to install .NET 6 on Ubuntu. 

Installing on Ubuntu requires installing libsnappy-dev, libc6-dev and librocksdb-dev via apt-get

``` shell
> sudo apt install libsnappy-dev libc6-dev librocksdb-dev -y
```

### MacOS Installation

Installing on MacOS requires installing rocksdb via [Homebrew](https://brew.sh/)

``` shell
> brew install rocksdb
```

#### Apple Silicon support

.NET 5 supports Macs with Apple Silicon via [Rosetta 2](https://support.apple.com/guide/security/rosetta-2-on-a-mac-with-apple-silicon-secebb113be1/1/web/1).
To run Neo-Express on a Mac with Apple Silicon, you need to [install homebrew under Rosetta](https://stackoverflow.com/questions/64882584/how-to-run-the-homebrew-installer-under-rosetta-2-on-m1-macbook/64883440#64883440) by using the `arch` command.

> Note, while NeoExpress has been upgraded to .NET 6, it has not been enabled for native execution on Apple Silicon yet.

``` shell
> arch -x86_64 /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/master/install.sh)"
```
One you have Homebrew installed under emulation, you can then run it under emulation to install rocksdb and it's dependencies. 
Note, if you have installed Homebrew both natively and under emulation, you'll need to provide the full path to the correct version.
As per [install instructions](https://docs.brew.sh/Installation), Homebrew is installed under /usr/local for macOS Intel and under /opt/homebrew for Apple Silicon.

``` shell
> arch -x86_64 /usr/local/bin/brew install rocksdb
```

### Install Preview Releases

Neo-Express has a public [build server](https://dev.azure.com/ngdenterprise/Build/_build?definitionId=2)
and [artifacts feed](https://dev.azure.com/ngdenterprise/Build/_packaging?_a=feed&feed=public%40Local).
You can install preview builds of Neo-express by specifying the nuget feed source
when running the dotnet tool install or update command.

``` shell
> dotnet tool install Neo.Express -g --add-source https://pkgs.dev.azure.com/ngdenterprise/Build/_packaging/public%40Local/nuget/v3/index.json --version <insert version>
```

Note, if the version isn't specified, the most recent release branch build will
be installed. For preview releases, the explicit version must be specified.
For more information, please see the
[dotnet tool install command](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install#options)
documentation.

## NeoTrace

In addition to NeoExpress, this repo contains the NeoTrace tool. NeoTrace is a tool
to generate [Neo Smart Contract Debugger](https://github.com/neo-project/neo-debugger)
trace files for existing blocks or transactions. You can specify a block by index or hash
or a transaction by hash.


```
> neotrace block 365110  --rpc-uri testnet
> neotrace block 0xd2421d88919dccc1ac73647bf06089bae78ce02060302eff861a04e381bc91ad  --rpc-uri testnet
> neotrace tx 0xef1917b8601828e1d2f3ed0954907ea611cb734771609ce0ce2b654bb5c78005 --rpc-uri testnet
```

NeoTrace depends on the Neo 3.0.3 [StateService plugin module](https://github.com/neo-project/neo-modules/tree/master/src/StateService)
running with `FullState` enabled. The official JSON-RPC nodes for MainNet and TestNet
(such as `http://seed1.neo.org:10332` and `http://seed1t4.neo.org:20332`) are configured to
run the StateService plugin with `FullState` enabled.

Like NeoExpress, NeoTrace is distributed as a [.NET Core Tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools).
It can be installed via the `dotnet tool` command.

``` shell
> dotnet tool install Neo.Trace -g
```

> Note, NeoTrace has no additional dependencies beyond .NET 6.

## A Message from the Engineer

Thanks for checking out NeoExpress and NeoTrace! I am eager to hear your opinion of the product.

If you like these tools, please let me know on [Twitter](https://twitter.com/devhawk),
[email](mailto:devhawk@outlook.com) or the [Neo Discord server](https://discord.gg/G5WEPwC).

If there are things about these tools you don't like, please file issues in our
[GitHub repo](https://github.com/neo-project/neo-express/issues). You can hit me up on
Twitter, Discord or email as well, but GitHub issues are how we track improvements
we make. So don't be shy - file an issue if there is anything you'd like to see changed in the product.

Most software is built by teams of people. However, NeoExpress and NeoTrace so far have been
a solo effort. I'm looking forward to having other folks contribute in the future,
but so far it's just been me. That means that these tools have been designed around
my experiences and my perspective. I can't help it, my perspective is the only
one I have! :) So while I find these tools intuitive, I realize that you may not
feel the same. Please let me know if this is the case! I didn't build these tools
for me, I built it for the Neo developer community at large. So if there are
changes we can make to make NeoExpress and/or NeoTrace more accessible, intuitive, easier to
use or just flat-out better - I want to hear about them.

Thanks again for checking out Neo-Express. I look forward to hearing from you.

\- Harry Pierson (aka [DevHawk](http://devhawk.net)), Chief Architect NGD Seattle
