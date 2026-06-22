// Copyright (C) 2015-2026 The Neo Project.
//
// FileNameUtilitiesTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit;
using Xunit;

namespace test.bctklib
{
    public class FileNameUtilitiesTests
    {
        [Theory]
        [InlineData("Contract.cs", "")]
        [InlineData("a", "")]
        [InlineData("", "")]
        [InlineData("/foo", "")]
        [InlineData("foo/bar", "foo")]
        [InlineData("foo/bar/baz", "foo/bar")]
        public void GetDirectoryName_handles_paths_without_a_parent_directory(string path, string expected)
        {
            FileNameUtilities.GetDirectoryName(path).Should().Be(expected);
        }

        [Theory]
        [InlineData("/", "")]
        [InlineData("//", "")]
        [InlineData("\\", "")]
        [InlineData("/a", "a")]
        [InlineData("//a", "a")]
        [InlineData("abc", "abc")]
        public void TrimStartDirectorySeparators_handles_all_separator_paths(string path, string expected)
        {
            FileNameUtilities.TrimStartDirectorySeparators(path).Should().Be(expected);
        }
    }
}
