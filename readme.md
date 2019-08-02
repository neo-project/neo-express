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

