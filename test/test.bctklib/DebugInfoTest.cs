// Copyright (C) 2015-2024 The Neo Project.
//
// DebugInfoTest.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

#pragma warning disable VSTHRD200
namespace test.bctklib
{
    public class DebugInfoTest
    {
        const string TEST_HASH = "0xf69e5188632deb3a9273519efc86cb68da8d42b8";

        [Fact]
        public void can_parse_no_docroot()
        {
            var text = Utility.GetResource("registrar-no-docroot.debug.json");
            var json = JObject.Parse(text);
            var debugInfo = DebugInfo.Parse(json);
            Assert.Equal(UInt160.Parse(TEST_HASH), debugInfo.ScriptHash);
            Assert.True(string.IsNullOrEmpty(debugInfo.DocumentRoot));
        }

        [Fact]
        public void can_parse_with_docroot()
        {
            var text = Utility.GetResource("registrar.debug.json");
            var json = JObject.Parse(text);
            var debugInfo = DebugInfo.Parse(json);
            Assert.Equal(UInt160.Parse(TEST_HASH), debugInfo.ScriptHash);
            Assert.False(string.IsNullOrEmpty(debugInfo.DocumentRoot));
        }

        [Fact]
        public void cant_parse_nccs_rc3()
        {
            var text = Utility.GetResource("nccs_rc3.json");
            var json = JObject.Parse(text);
            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void can_parse_minimal_debug_info()
        {
            var json = new JObject(new JProperty("hash", TEST_HASH));

            var debugInfo = DebugInfo.Parse(json);
            Assert.Equal(UInt160.Parse(TEST_HASH), debugInfo.ScriptHash);
            Assert.True(string.IsNullOrEmpty(debugInfo.DocumentRoot));
            Assert.Empty(debugInfo.Documents);
            Assert.Empty(debugInfo.Events);
            Assert.Empty(debugInfo.Methods);
            Assert.Empty(debugInfo.StaticVariables);
        }

        [Fact]
        public void cant_parse_without_hash()
        {
            var debugInfoJson = Utility.GetResource("registrar.debug.json");
            var json = JObject.Parse(debugInfoJson);
            json.Remove("hash");

            var ex = Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
            Assert.Equal("Missing hash value", ex.Message);
        }

        [Fact]
        public void can_parse_static_variables()
        {
            var json = new JObject(
                new JProperty("hash", TEST_HASH),
                new JProperty("static-variables",
                    new JArray(
                        "testStatic1,String",
                        "testStatic2,Hash160")));

            var debugInfo = DebugInfo.Parse(json);

            Assert.Collection(debugInfo.StaticVariables,
                s =>
                {
                    Assert.Equal("testStatic1", s.Name);
                    Assert.Equal("String", s.Type);
                    Assert.Equal(0, s.Index);
                },
                s =>
                {
                    Assert.Equal("testStatic2", s.Name);
                    Assert.Equal("Hash160", s.Type);
                    Assert.Equal(1, s.Index);
                });
        }

        [Fact]
        public void can_parse_static_variables_explicit_slot_indexes()
        {
            var json = new JObject(
                new JProperty("hash", TEST_HASH),
                new JProperty("static-variables",
                    new JArray(
                        "testStatic1,String,1",
                        "testStatic2,Hash160,3")));

            var debugInfo = DebugInfo.Parse(json);

            Assert.Collection(debugInfo.StaticVariables,
                s =>
                {
                    Assert.Equal("testStatic1", s.Name);
                    Assert.Equal("String", s.Type);
                    Assert.Equal(1, s.Index);
                },
                s =>
                {
                    Assert.Equal("testStatic2", s.Name);
                    Assert.Equal("Hash160", s.Type);
                    Assert.Equal(3, s.Index);
                });
        }

        [Fact]
        public void cant_parse_when_mix_and_match_optional_slot_index()
        {
            var json = new JObject(
                new JProperty("hash", TEST_HASH),
                new JProperty("static-variables",
                    new JArray(
                        "testStatic1,String,1",
                        "testStatic2,Hash160")));

            Assert.Throws<FormatException>(() => DebugInfo.Parse(json));
        }

        [Fact]
        public void can_parse_debug_info_with_invalid_sequence_points()
        {
            var debugInfoJson = Utility.GetResource("invalidSequencePoints.json");
            var json = JObject.Parse(debugInfoJson);
            var debug = DebugInfo.Parse(json);
        }

        [Fact]
        public async Task can_load_debug_json()
        {
            var debugInfoJson = Utility.GetResource("Registrar.debug.json");
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            string nefPath = fileSystem.Path.Combine(rootPath, "fakeContract.nef");
            fileSystem.AddFile(nefPath, new MockFileData(string.Empty));
            fileSystem.AddFile(fileSystem.Path.Combine(rootPath, "fakeContract.debug.json"), new MockFileData(debugInfoJson));
            var debugInfo = await DebugInfo.LoadContractDebugInfoAsync(nefPath, null, fileSystem);
            Assert.True(debugInfo.IsT0);
        }

        [Fact]
        public async Task can_load_nefdbgnfo()
        {
            var debugInfoJson = Utility.GetResource("Registrar.debug.json");
            var compressedDebugInfo = CreateCompressedDebugInfo("fakeContract", debugInfoJson);
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            string nefPath = fileSystem.Path.Combine(rootPath, "fakeContract.nef");
            fileSystem.AddFile(nefPath, new MockFileData(string.Empty));
            fileSystem.AddFile(fileSystem.Path.Combine(rootPath, "fakeContract.nefdbgnfo"), new MockFileData(compressedDebugInfo));
            var debugInfo = await DebugInfo.LoadContractDebugInfoAsync(nefPath, null, fileSystem);
            Assert.True(debugInfo.IsT0);
        }

        [Fact]
        public async Task not_found_debug_info()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            string nefPath = fileSystem.Path.Combine(rootPath, "fakeContract.nef");
            var debugInfo = await DebugInfo.LoadContractDebugInfoAsync(nefPath, null, fileSystem);
            Assert.True(debugInfo.IsT1);
        }

        [Fact]
        public void resolve_source_current_directory()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var srcPath = fileSystem.Path.Combine(rootPath, "src");
            fileSystem.Directory.SetCurrentDirectory(srcPath);
            var apocPath = fileSystem.Path.Combine(srcPath, "Apoc.cs");
            fileSystem.AddFile(apocPath, new MockFileData(string.Empty));
            fileSystem.AddFile(fileSystem.Path.Combine(srcPath, "Apoc.Crowdsale.cs"), new MockFileData(string.Empty));

            var testPath = @"c:\Users\harry\Source\neo\seattle\samples\token-sample\src\Apoc.cs";
            var sourceMap = new Dictionary<string, string>();

            var actual = DebugInfo.ResolveDocument(testPath, sourceMap, fileSystem);
            Assert.Equal(apocPath, actual);
        }

        [Fact]
        public void resolve_source_files_exist()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var srcPath = fileSystem.Path.Combine(rootPath, "neo", "token-sample", "src");
            fileSystem.Directory.SetCurrentDirectory(srcPath);
            var apocPath = fileSystem.Path.Combine(srcPath, "Apoc.cs");
            fileSystem.AddFile(apocPath, new MockFileData(string.Empty));
            var sourceMap = new Dictionary<string, string>();

            var actual = DebugInfo.ResolveDocument(apocPath, sourceMap, fileSystem);
            Assert.Equal(apocPath, actual);
        }

        [Fact]
        public void resolve_source_files_dont_exist()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var srcPath = fileSystem.Path.Combine(rootPath, "neo", "token-sample", "src");
            fileSystem.Directory.SetCurrentDirectory(srcPath);
            var apocPath = fileSystem.Path.Combine(srcPath, "Apoc.cs");
            var sourceMap = new Dictionary<string, string>();

            var actual = DebugInfo.ResolveDocument(apocPath, sourceMap, fileSystem);
            Assert.Equal(apocPath, actual);
        }

        [Fact]
        public void resolve_source_via_map()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var srcPath = fileSystem.Path.Combine(rootPath, "neo", "token-sample", "src");
            var apocPath = fileSystem.Path.Combine(srcPath, "Apoc.cs");
            fileSystem.AddFile(apocPath, new MockFileData(string.Empty));

            var sourceMap = new Dictionary<string, string>
            {
                { @"c:\Users\harry\Source\neo\seattle\samples\token-sample\src", srcPath}
            };
            var actual = DebugInfo.ResolveDocument(apocPath, sourceMap, fileSystem);
            Assert.Equal(apocPath, actual);
        }

        [Fact]
        public void resolve_source_via_map_2()
        {
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var tokenSamplePath = fileSystem.Path.Combine(rootPath, "neo", "token-sample");
            var apocPath = fileSystem.Path.Combine(tokenSamplePath, "src", "Apoc.cs");
            fileSystem.AddFile(apocPath, new MockFileData(string.Empty));

            var sourceMap = new Dictionary<string, string>
            {
                { @"c:\Users\harry\Source\neo\seattle\samples\token-sample", tokenSamplePath}
            };
            var actual = DebugInfo.ResolveDocument(apocPath, sourceMap, fileSystem);
            Assert.Equal(apocPath, actual);
        }

        static byte[] CreateCompressedDebugInfo(string contractName, string debugInfo)
        {
            var jsonDebugInfo = Neo.Json.JToken.Parse(debugInfo) ?? throw new NullReferenceException();
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                using var stream = archive.CreateEntry($"{contractName}.debug.json").Open();
                stream.Write(jsonDebugInfo.ToByteArray(false));
            }
            return memoryStream.ToArray();
        }
    }
}
#pragma warning restore VSTHRD200
