// Copyright (C) 2015-2026 The Neo Project.
//
// FileSystemExtensionsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace test.workflowvalidation;

public class FileSystemExtensionsTests
{
    [Fact]
    public async Task LoadContractAsync_rejects_oversized_nef_file()
    {
        var fileSystem = CreateFileSystem();
        var contractPath = "/work/contract.nef";
        var manifestPath = "/work/contract.manifest.json";
        fileSystem.AddFile(contractPath, new MockFileData(new byte[FileSystemExtensions.MaxContractFileBytes + 1]));
        fileSystem.AddFile(manifestPath, new MockFileData(ValidManifestJson));

        Func<Task> action = () => fileSystem.LoadContractAsync(contractPath);

        var exception = await action.Should().ThrowAsync<Exception>();
        exception.Which.Message.Should().Be($"Contract file '{contractPath}' is invalid: file is larger than {FileSystemExtensions.MaxContractFileBytes} bytes");
    }

    [Fact]
    public async Task LoadContractAsync_rejects_oversized_manifest_file()
    {
        var fileSystem = CreateFileSystem();
        var contractPath = "/work/contract.nef";
        var manifestPath = "/work/contract.manifest.json";
        fileSystem.AddFile(contractPath, new MockFileData(ValidNefBytes()));
        fileSystem.AddFile(manifestPath, new MockFileData(new string(' ', (int)FileSystemExtensions.MaxContractFileBytes + 1)));

        Func<Task> action = () => fileSystem.LoadContractAsync(contractPath);

        var exception = await action.Should().ThrowAsync<Exception>();
        exception.Which.Message.Should().Be($"Contract manifest '{manifestPath}' is invalid: file is larger than {FileSystemExtensions.MaxContractFileBytes} bytes");
    }

    private static MockFileSystem CreateFileSystem()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>(), "/work");
        fileSystem.Directory.CreateDirectory("/work");
        return fileSystem;
    }

    private static byte[] ValidNefBytes()
    {
        var solutionDir = FindSolutionDirectory(Directory.GetCurrentDirectory());
        return File.ReadAllBytes(Path.Combine(solutionDir, "test", "test.bctklib", "_testFiles", "registrar.nef"));
    }

    private static string FindSolutionDirectory(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory != null)
        {
            if (directory.GetFiles("neo-express.sln").Any())
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find neo-express.sln");
    }

    private const string ValidManifestJson = """
        {
          "name":"SampleContract",
          "groups":[],
          "features":{},
          "supportedstandards":[],
          "abi":{
            "methods":[{"name":"dummy","parameters":[],"returntype":"Void","offset":0,"safe":false}],
            "events":[]
          },
          "permissions":[],
          "trusts":[],
          "extra":{}
        }
        """;
}
