// Copyright (C) 2015-2026 The Neo Project.
//
// ResolveFileNameTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace test.bctklib
{
    public class ResolveFileNameTests
    {
        [Fact]
        public void empty_fileName_uses_the_default_callback()
        {
            var fs = new MockFileSystem();
            var cwd = fs.Directory.GetCurrentDirectory();

            fs.ResolveFileName("", ".neo-express", () => "default.neo-express")
                .Should().Be(fs.Path.Combine(cwd, "default.neo-express"));
        }

        [Fact]
        public void bare_name_is_qualified_against_the_current_directory_and_extension_appended()
        {
            var fs = new MockFileSystem();
            var cwd = fs.Directory.GetCurrentDirectory();

            fs.ResolveFileName("foo", ".neo-express", () => "unused")
                .Should().Be(fs.Path.Combine(cwd, "foo.neo-express"));
        }

        [Fact]
        public void matching_extension_is_case_insensitive_and_not_doubled()
        {
            var fs = new MockFileSystem();
            var cwd = fs.Directory.GetCurrentDirectory();

            fs.ResolveFileName("foo.NEO-EXPRESS", ".neo-express", () => "unused")
                .Should().Be(fs.Path.Combine(cwd, "foo.NEO-EXPRESS"));
        }

        [Fact]
        public void fully_qualified_path_keeps_its_directory()
        {
            var fs = new MockFileSystem();
            var input = fs.Path.Combine(fs.Path.GetTempPath(), "bar");

            fs.ResolveFileName(input, ".neo-express", () => "unused")
                .Should().Be(input + ".neo-express");
        }
    }
}
