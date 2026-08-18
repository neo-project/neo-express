// Copyright (C) 2015-2026 The Neo Project.
//
// RepositoryVersionConsistencyTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace test.workflowvalidation;

public class RepositoryVersionConsistencyTests
{
    [Fact]
    public void Repository_assets_use_current_version()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var releaseVersion = ReadReleaseVersion(repositoryRoot);

        ReadPropertyVersion(repositoryRoot, "src/Directory.Build.props", "NeoVersion")
            .Should().Be(releaseVersion);
        ReadPropertyVersion(repositoryRoot, "src/Directory.Build.props", "NeoModuleVersion")
            .Should().Be(releaseVersion);
        ReadPropertyVersion(repositoryRoot, "samples/src/contract.csproj", "NeoTestVersion")
            .Should().Be(releaseVersion);
        ReadPropertyVersion(repositoryRoot, "samples/test/contract-test.csproj", "NeoTestVersion")
            .Should().Be(releaseVersion);

        ReadToolVersion(repositoryRoot, "samples/.config/dotnet-tools.json", "neo.express")
            .Should().Be(releaseVersion);
        ReadToolVersion(repositoryRoot, "samples/.config/dotnet-tools.json", "neo.compiler.csharp")
            .Should().Be(releaseVersion);
        ReadToolVersion(repositoryRoot, "extentions/neo3-visual-tracker/resources/new-contract/csharp/.config/dotnet-tools.json", "neo.express")
            .Should().Be(releaseVersion);
        ReadToolVersion(repositoryRoot, "extentions/neo3-visual-tracker/resources/new-contract/csharp/.config/dotnet-tools.json", "neo.compiler.csharp")
            .Should().Be(releaseVersion);
        ReadToolVersion(repositoryRoot, "extentions/neo3-visual-tracker/sample-workspaces/sample-workspace/.config/dotnet-tools.json", "neo.express")
            .Should().Be(releaseVersion);
        ReadToolVersion(repositoryRoot, "extentions/neo3-visual-tracker/sample-workspaces/sample-workspace/.config/dotnet-tools.json", "neo.compiler.csharp")
            .Should().Be(releaseVersion);

        ReadPackageVersion(repositoryRoot, "extentions/neo3-visual-tracker/resources/new-contract/csharp/src/$_CLASSNAME_$.csproj.template.txt", "Neo.SmartContract.Framework")
            .Should().Be(releaseVersion);
        ReadPackageVersion(repositoryRoot, "extentions/neo3-visual-tracker/resources/new-contract/csharp/src/$_CLASSNAME_$.csproj.template.txt", "Neo.BuildTasks")
            .Should().Be(releaseVersion);
        ReadPackageVersion(repositoryRoot, "extentions/neo3-visual-tracker/sample-workspaces/sample-workspace/Sample/SampleContract.csproj", "Neo.SmartContract.Framework")
            .Should().Be(releaseVersion);
        ReadPackageVersion(repositoryRoot, "extentions/neo3-visual-tracker/sample-workspaces/sample-workspace/Sample/SampleContract.csproj", "Neo.BuildTasks")
            .Should().Be(releaseVersion);
    }

    private static string FindRepositoryRoot(string startPath)
    {
        for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "version.json"))
                && File.Exists(Path.Combine(directory.FullName, "neo-express.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException($"Could not find the repository root from {startPath}.");
    }

    private static string ReadReleaseVersion(string repositoryRoot)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "version.json")));
        return document.RootElement.GetProperty("version").GetString()
            ?? throw new InvalidDataException("version.json does not contain a version.");
    }

    private static string ReadToolVersion(string repositoryRoot, string relativePath, string toolName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repositoryRoot, relativePath)));
        return document.RootElement
            .GetProperty("tools")
            .GetProperty(toolName)
            .GetProperty("version")
            .GetString()
            ?? throw new InvalidDataException($"{relativePath} does not contain a version for {toolName}.");
    }

    private static string ReadPackageVersion(string repositoryRoot, string relativePath, string packageName)
    {
        var document = XDocument.Load(Path.Combine(repositoryRoot, relativePath));
        return document.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Single(element => (string?)element.Attribute("Include") == packageName)
            .Attribute("Version")?.Value
            ?? throw new InvalidDataException($"{relativePath} does not contain a version for {packageName}.");
    }

    private static string ReadPropertyVersion(string repositoryRoot, string relativePath, string propertyName)
    {
        var document = XDocument.Load(Path.Combine(repositoryRoot, relativePath));
        return document.Descendants()
            .SingleOrDefault(element => element.Name.LocalName == propertyName)?.Value
            ?? throw new InvalidDataException($"{relativePath} does not contain {propertyName}.");
    }
}
