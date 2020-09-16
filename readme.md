<!-- markdownlint-enable -->
# Neo-Express
[![Build Status](https://dev.azure.com/NGDSeattle/Public/_apis/build/status/neo-project.neo-express?branchName=master)](https://dev.azure.com/NGDSeattle/Public/_build/latest?definitionId=24&branchName=master)
[![Nuget](https://img.shields.io/nuget/v/Neo.Express)](https://www.nuget.org/packages/Neo.Express/)

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
[Command Reference](https://neo-project.github.io/neo-express/command-reference)
to get an understanding of Neo-Express capabilities. There is also a
[Quickstart](https://neo-project.github.io/neo-blockchain-toolkit/quickstart)
for the [Neo Blockchain Toolkit](https://marketplace.visualstudio.com/items?itemName=ngd-seattle.neo-blockchain-toolkit)
that covers Neo-Express as well.

## A Message from the Engineer

Thanks for checking out Neo-Express! I am eager to hear your opinion of the product.

If you like Neo-Express, please let me know on [Twitter](https://twitter.com/devhawk),
[email](mailto:devhawk@outlook.com) or the [Neo Discord server](https://discord.gg/G5WEPwC).

If there are things about Neo-Express you don't like, please file issues in our
[GitHub repo](https://github.com/neo-project/neo-express/issues). You can hit me up on
Twitter, Discord or email as well, but GitHub issues are how we track improvements
we make to Neo-Express. So don't be shy - file an issue if there is anything
you'd like to see changed in the product.

Most software is built by teams of people. However, Neo-Express so far has been
a solo effort. I'm looking forward to having other folks contribute in the future,
but so far it's just been me. That means that Neo-Express has been designed around
my experiences and my perspective. I can't help it, my perspective is the only
one I have! :) So while I find Neo-Express intuitive, I realize that you may not
feel the same. Please let me know if this is the case! I didn't build Neo-Express
for me, I built it for the Neo developer community at large. So if there are
changes we can make to make Neo-Express more accessible, intuitive, easier to
use or just flat-out better - I want to hear about them.

Thanks again for checking out Neo-Express. I look forward to hearing from you.

\- Harry Pierson (aka [DevHawk](http://devhawk.net)), Chief Architect NGD Seattle

## Installation

Neo-Express is distributed as a
[.NET Core Global Tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools).
Different versions of Neo-Express require different versions of .NET Core.

|Neo-Express Version|.NET Core Version|
|-------------------|-----------------|
| v1.1 | [v3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) |
| v1.0 | [v3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1) |
| v0.9 | [v3.0](https://dotnet.microsoft.com/download/dotnet-core/3.0) |
| v0.8 | [v2.2](https://dotnet.microsoft.com/download/dotnet-core/2.2) |

> As of v1.0, Neo-Express has snapped to a Long Term Support (LTS) release of
> .NET Core. .NET Core LTS releases are
> [supported for three years](https://github.com/dotnet/core/blob/master/microsoft-support.md#long-term-support-lts-releases).
> The next LTS release of .NET Core isn't projected be released until
> [November 2021](https://github.com/dotnet/core/blob/master/roadmap.md#upcoming-ship-dates),
> so we expect to stay on this version of .NET core for at least two years.

To install Neo-Express, open a terminal window and enter the following command:

``` shell
> dotnet tool install Neo.Express -g
```

To upgrade Neo-Express, enter the following command in a terminal window:

``` shell
> dotnet tool update Neo.Express -g
```

### Ubuntu Installation

Installing on Ubuntu 18.04 requires installing libsnappy-dev and libc6-dev via apt-get

``` shell
> sudo apt install libsnappy-dev libc6-dev -y
```

### MacOS Installation

Installing on MacOS requires installing rocksdb via [Homebrew](https://brew.sh/)

``` shell
> brew install rocksdb
```

### Install Preview Releases

Neo-Express has a public [build server](https://dev.azure.com/NGDSeattle/Public/_build?definitionId=24)
and [NuGet feed](https://dev.azure.com/NGDSeattle/Public/_packaging?_a=package&feed=NeoPublicPackages&package=Neo.Express&protocolType=NuGet).
You can install preview builds of Neo-express by specifying the nuget feed source
when running the dotnet tool install or update command.

``` shell
> dotnet tool install Neo.Express -g --add-source https://pkgs.dev.azure.com/NGDSeattle/Public/_packaging/NeoPublicPackages/nuget/v3/index.json --version <insert version>
```

Note, if the version isn't specified, the most recent release branch build will
be installed. For preview releases, the explicit version must be specified.
For more information, please see the
[dotnet tool install command](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install#options)
documentation.
