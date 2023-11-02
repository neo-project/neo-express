# Neo-Express Installation

Neo-Express and Neo-Trace are distributed as [.NET Tools](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools). .NET tools are NuGet packages containing console applications that can be installed on a developer's machine via the `dotnet tool` command.

To install the latest version of Neo-Express as a global tool, run the following command in a terminal window:

```shell
> dotnet tool install Neo.Express -g
```

To update Neo-Express to the latest version, run the following command in a terminal window:

```shell
> dotnet tool update Neo.Express -g
```

> **Note**: The process for installing and updating Neo-Trace is identical to Neo-Express, except the Neo-Trace NuGet package is named `Neo.Trace`.

.NET tools also support "local tool" installation, allowing different versions of a .NET tool to be installed in different directories. You can find full details on installing and updating .NET tools in the [official documentation](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools).

## Installing Preview Releases

The Neo Blockchain Toolkit has a public [build server](https://dev.azure.com/ngdenterprise/Build/_build) and [package feed](https://dev.azure.com/ngdenterprise/Build/_artifacts). The public package feed contains unreleased builds of Neo-Express and Neo-Trace.

To install preview builds of Neo-Express or Neo-Trace, you can use the `--add-source` option to specify the Neo Blockchain Toolkit package feed. For example, to update to the latest release branch version of Neo-Express, run this command:

```shell
> dotnet tool update Neo.Express -g --add-source https://pkgs.dev.azure.com/ngdenterprise/Build/_packaging/public/nuget/v3/index.json
```

You can also install master branch releases of these tools by using the `--version` and/or `--prerelease` command line options. For more details, please see the [official dotnet tool documentation](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools#install-a-specific-tool-version).

If you regularly use unreleased versions of these tools in a specific project, you can specify the Neo Blockchain Toolkit package feed in a [NuGet.config file](https://docs.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior#changing-config-settings). Several Neo sample projects, like [NeoContributorToken](https://github.com/ngdenterprise/neo-contrib-token), use a NuGet.config file.
```

This revised version improves the formatting and organization of the original document.