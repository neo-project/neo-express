<!-- markdownlint-enable -->
# Neo-Express and Neo-Trace

[![Nuget](https://img.shields.io/nuget/v/Neo.Express)](https://www.nuget.org/packages/Neo.Express/)
[![Build Status](https://dev.azure.com/ngdenterprise/Build/_apis/build/status/neo-project.neo-express?branchName=master)](https://dev.azure.com/ngdenterprise/Build/_build/latest?definitionId=2&branchName=master)

> Note, This repo uses a branch structure similar to other repos in the Neo project.
> The `master` branch contains Neo N3 version of Neo-Express and Neo-Trace.
> The `master-2.x` branch contains Neo Legacy version of Neo-Express.
> There is no Neo Legacy version of Neo-Trace.

## Installation

Developers can choose installing Neo-Express from release packages or using .NET tool.

### Installing release package

Download the latest release package from [neo-express releases](https://github.com/neo-project/neo-express/releases) for your operation system, and unzip it in your local computer.

Configure the path of neoxp.exe to the environment variable in your computer. 

### Installing using .NET Tool

Neo-Express and Neo-Trace are distributed as
[.NET Tools](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools).
.NET tools are [NuGet](https://nuget.org) packages containing console applications
that can be installed on a developer's machine via the `dotnet tool` command.

#### Requirements

As of Neo v3.6, Neo-Express and Neo-Trace require 
[version 7.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) 
of [the .NET developer platform](https://dot.net) to be installed. 

> Note: Neo-Express has additional, platform-specific requirements beyond .NET 7.
> These requirements are detailed below.
> Neo-Trace has no additional dependencies beyond .NET 7.

> Note: the Neo v3.0 version of Neo-Express and Neo-Trace used .NET 5.
> .NET 5 is [no longer supported](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) by Microsoft.
> We strongly recommend using .NET 7 and the latest version of Neo-Express and Neo-Trace.

#### Installation

To install the latest version of Neo-Express as a global tool, run the
`dotnet tool install` command in a terminal window.

``` shell
> dotnet tool install Neo.Express -g
```

To update Neo-Express to the latest version, run the `dotnet tool update`
command in a terminal window.

``` shell
> dotnet tool update Neo.Express -g
```

> Note: The process for installing and updating Neo-Trace is identical to Neo-Express
> except the Neo-Trace NuGet package is `Neo.Trace`.

.NET tools also supports "local tool" installation. This allows for different
versions of a .NET tool to be installed in different directories.
Full details on installing and updating .NET tools are available in the
[official documentation](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools).

### Installing Preview Releases

The Neo Blockchain Toolkit has a public
[build server](https://dev.azure.com/ngdenterprise/Build/_build) and
[package feed](https://dev.azure.com/ngdenterprise/Build/_artifacts).
The public package feed contains unreleased builds of Neo-Express and Neo-Trace.

You can install preview builds of Neo-Express or Neo-Trace by using the `--add-source`
option to specify the Neo Blockchain Toolkit package feed.
For example, to update to the latest release branch version of Neo-Express, you would run this command:

``` shell
> dotnet tool update Neo.Express -g --add-source https://www.myget.org/F/neo/api/v3/index.json
```

You can also install master branch releases of these tools by using the `--version`
and/or `--prerelease` command line options. For more details, please see the
[official dotnet tool documentation](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools#install-a-specific-tool-version).

If you regularly use unreleased versions of these tools in a given project,
you can specify the Neo Blockchain Toolkit package feed in a 
[NuGet.config file](https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior#changing-config-settings).
Several Neo sample projects like 
[NeoContributorToken](https://github.com/ngdenterprise/neo-contrib-token)
use a NuGet.config file.

## Neo-Express

Neo-Express is a privatenet optimized for development scenarios. 
It is built on the same Neo platform core as
[neo-cli](https://docs.neo.org/docs/en-us/node/cli/setup.html) and
[neo-gui](https://docs.neo.org/docs/en-us/node/gui/install.html)
to maximize compatibility between local development and public chain environments.

Neo-Express provides the following features:

- Blockchain instance management
- Wallet management
- Asset management
- Smart contract management
- Blockchain checkpoint and rollback

### Additional Neo-Express Requirements

#### Ubuntu Installation

> Note, while Microsoft has instructions for 
> [installing .NET via Snap](https://docs.microsoft.com/en-us/dotnet/core/install/linux-snap),
> there is a [known issue](https://github.com/dotnet/runtime/issues/3775#issuecomment-534263315)
> with this approach that leads to a segmentation fault in Neo Express.
> Unfortunately, this issue with the .NET snap installer
> [has been closed and will not be fixed](https://github.com/dotnet/runtime/issues/3775#issuecomment-888676286).
> As such, we recommend [using APT](https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu)
> to install .NET on Ubuntu instead.

Installing on Ubuntu requires installing libsnappy-dev, libc6-dev and librocksdb-dev via apt-get

``` shell
> sudo apt install libsnappy-dev libc6-dev librocksdb-dev -y
```

#### MacOS Installation

Installing on MacOS requires installing rocksdb via [Homebrew](https://brew.sh/)

``` shell
> brew install rocksdb
```

> Note, .NET 6 Arm64 has [full support for Apple Silicon](https://devblogs.microsoft.com/dotnet/announcing-net-6/#arm64).
> Homebrew likewise also supports Apple Silicon. If you have any issues running Neo-Express on Apple Silicon hardware,
> please [open an issue](https://github.com/neo-project/neo-express/issues) in the Neo-Express repo.

#### Neo Legacy Version Support

Neo Legacy versions of Neo-Express used older versions of .NET Core.

> Note, if you need a Neo Legacy version of Neo-Express because you are still
> developing for the Legacy Neo Blockchain, we highly advise using the v1.1
> version of Neo-Express. Pre-release versions of the Neo Legacy version of
> Neo-Express ran on versions of .NET Core that no longer supported by Microsoft.

| Neo-Express Version | .NET Core Version                                            |
| ------------------- | ------------------------------------------------------------ |
| v1.1                | [v3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) |
| v1.0                | [v3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) |
| v0.9                | [v3.0](https://dotnet.microsoft.com/download/dotnet-core/3.0) |
| v0.8                | [v2.2](https://dotnet.microsoft.com/download/dotnet-core/2.2) |

## Neo-Trace

Neo-Trace is a tool to generate
[Neo Smart Contract Debugger](https://github.com/neo-project/neo-debugger)
trace files for existing blocks or transactions. You can specify a block by index or hash
or a transaction by hash.

```
> neotrace block 365110 --rpc-uri testnet
> neotrace block 0xd2421d88919dccc1ac73647bf06089bae78ce02060302eff861a04e381bc91ad --rpc-uri testnet
> neotrace tx 0xef1917b8601828e1d2f3ed0954907ea611cb734771609ce0ce2b654bb5c78005--rpc-uri testnet
```

Neo-Trace depends on the  
[StateService plugin module](https://github.com/neo-project/neo-modules/tree/master/src/StateService)
running with `FullState` enabled. The official JSON-RPC nodes for MainNet and TestNet
(such as `http://seed1.neo.org:10332` and `http://seed1t5.neo.org:20332`) are configured to
run the StateService plugin with `FullState` enabled.

## Documentation

Docs are somewhat limited at this point. Please review the
[Command Reference](docs/command-reference.md) to get an understanding of
Neo-Express capabilities.

## New Features or issues

Thanks for checking out Neo-Express and Neo-Trace!  We are eager to hear your opinion to make these tools more accessible, intuitive, easier to use or just flat-out better.

GitHub issues are how we track improvements we make. If you found a problem when using these tools, or there is anything you'd like to see changed in the product, check the [issues page](https://github.com/neo-project/neo-express/issues) to see if the bug or change was already reported, if not just file an issue.

When creating a new issue, try to keep the title and description concise, and give some context of the issue, like a snippet of the code where the problem is happening or a example of a feature, and its expected behavior.
