<!-- markdownlint-enable -->
# Neo-Express

Neo-Express is a new Neo blockchain client application that is optimized for
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

Please note, Neo-Express is in preview. There is more work to be done and there
are assuredly bugs in the product. Please let us know of any issues you find via
our [GitHub repo](https://github.com/neo-project/neo-express/issues).

Docs are somewhat limited at this point. Please review the
[Command Reference](docs\command-reference.md) to get an understanding of
Neo-Express capabilities.

## A Message from the Engineer

Thanks for checking out Neo-Express! I am eager to hear your opinion of the product.

If you like Neo-Express, please let me know on [Twitter](https://twitter.com/devhawk),
[email](mailto:devhawk@outlook.com) or the [Neo Discord server](https://discord.gg/G5WEPwC).

If there are things about Neo-Express you don't like, please file issues in our
[GitHub repo](https://github.com/neo-project/neo-express/issues). You can hit me up on
Twitter, Discord or email as well, but GitHub issues are how we track improvements
we make to Neo-Express. So don't be shy - file an issue if there is anything
you'd like to see changed in the product.

Before you get started, I'd just like to point out again that Neo-Express is
currently in preview. That means that you are more likely to find bugs or run
across incomplete features than you would in other software. Furthermore, Neo-Express
may not work in a way that you find intuitive.

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
Global Tools require at least .NET Core 2.2 installed. If you don't have .NET
Core 2.2 or later installed, you can install it from [the .NET website](https://dotnet.microsoft.com/).

> Note, Neo-Express intends to snap to Long Term Support (LTS) releases of .NET Core.
> However, the current LTS release of .NET Core (v2.1), can't run neo.dll on the
> Windows Subsystem for Linux due to a [bug](https://github.com/dotnet/corefx/issues/26476).
> Because of this bug, Neo-Express is built against .NET Core 2.2.
>
> As per the .NET Core [support policy](https://github.com/dotnet/core/blob/master/microsoft-support.md#current-releases)
> and [road map](https://github.com/dotnet/core/blob/3604c1ca961b61cb32d293056c77b40230f98a67/roadmap.md#upcoming-ship-dates),
> .NET Core v2.2 support is scheduled to end in December 2019. Neo-Express will move
> to the next LTS version of .NET Core - scheduled to be v3.1 in November 2019 -
> as soon as it is available.  

To install Neo-Express, open a terminal window and enter the following command:

``` shell
dotnet tool install Neo.Express -g
```

> Installing on Ubuntu 18.04 requires libsnappy-dev and libc6-dev
