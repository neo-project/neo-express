<!-- markdownlint-enable -->
# NEO-Express

NEO-Express is a new NEO blockchain client application that is optimized for
development scenarios. It is built on the same NEO platform core as
[NEO-CLI](https://docs.neo.org/docs/en-us/node/cli/setup.html) and
[NEO-GUI](https://docs.neo.org/docs/en-us/node/gui/install.html) ensuring that
blockchain application code runs the same in production as it does in the
NEO-Express development environment.

NEO-Express provides the following features:

- Blockchain instance management
- Wallet management
- Asset management
- Smart contract management
- Blockchain checkpoint and rollback

Please note, NEO-Express is in preview. There is more work to be done and there
are assuredly bugs in the product. Please let us know of any issues you find via
our [GitHub repo](https://github.com/ngdseattle/neo-express).

Docs are somewhat limited at this point. Please review the
[Command Reference](docs\command-reference.md) to get an understanding of
NEO-Express capabilities.

## A Message from the Engineer

Thanks for checking out NEO-Express! I am eager to hear your opinion of the product.

If you like NEO-Express, please let me know on [Twitter](https://twitter.com/devhawk),
[email](mailto:devhawk@outlook.com) or the [NEO Discord server](https://discord.gg/G5WEPwC).

If there are things about NEO-Express you don't like, please file issues in our
[GitHub repo](https://github.com/ngdseattle/neo-express). You can hit me up on
Twitter, Discord or email as well, but GitHub issues are how we track improvements
we make to NEO-Express. So don't be shy - file an issue if there is anything
you'd like to see changed in the product.

Before you get started, I'd just like to point out again that NEO-Express is
currently in preview. That means that you are more likely to find bugs or run
across incomplete features than you would in other software. Furthermore, NEO-Express
may not work in a way that you find intuitive.

Most software is built by teams of people. However, NEO-Express so far has been
a solo effort. I'm looking forward to having other folks contribute in the future,
but so far it's just been me. That means that NEO-Express has been designed around
my experiences and my perspective. I can't help it, my perspective is the only
one I have! :) So while I find NEO-Express intuitive, I realize that you may not
feel the same. Please let me know if this is the case! I didn't build NEO-Express
for me, I built it for the NEO developer community at large. So if there are
changes we can make to make NEO-Express more accessible, intuitive, easier to
use or just flat-out better - I want to hear about them.

Thanks again for checking out NEO-Express. I look forward to hearing from you.

\- Harry Pierson (aka [DevHawk](http://devhawk.net)), Chief Architect NGD Seattle

## Installation

NEO-Express is distributed as a
[.NET Core Global Tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools).
Global Tools require at least .NET Core 2.1 installed. If you don't have .NET
Core 2.1 or later installed, you can install it from [the .NET website](https://dotnet.microsoft.com/).

> Note, we need to decide on a distribution channel before completing the install section


Eventually, NEO-Express will be distributed on nuget.org

To install NEO-Express, open a command windows 

``` shell
dotnet tool install neo-express -g --add-source https://sleettest.blob.core.windows.net/myfeed2/index.json


--version <insert version number here>
```

Because NEO-Express is currently in preview, you need to specify the version you
wish to install manually. If you attempt to install neo-express without specifying
the version, the dotnet CLI tool will report the most recent version it could find.
You can then specify the version to install when you re-run the install command. 

> Note, the current version of NEO-Express as of this writing is `0.5.0-preview-20190801.3`.
> However, a later version may be available when you go to install it.

``` shell
$> dotnet tool install neo-express -g 
error NU1103: Unable to find a stable package neo-express with version
error NU1103:   - Found 0 version(s) in Microsoft Visual Studio Offline Packages
error NU1103:   - Found 0 version(s) in C:\Program Files\dotnet\sdk\NuGetFallbackFolder
error NU1103:   - Found 2 version(s) in nuget.org
                  [ Nearest version: 0.5.0-preview-20190801.3 ]
The tool package could not be restored.
Tool 'neo-express' failed to install. This failure may have been caused by:

* You are attempting to install a preview release and did not use the --version option to specify the version.
* A package by this name was found, but it was not a .NET Core tool.
* The required NuGet feed cannot be accessed, perhaps because of an Internet connection problem.
* You mistyped the name of the tool.

$> dotnet tool install neo-express -g --version 0.5.0-preview-20190801.3
You can invoke the tool using the following command: neo-express
Tool 'neo-express' (version '0.5.0-preview-20190801.3') was successfully installed.
```

> Installing on Ubuntu 18.04 requires libsnappy-dev and libc6-dev