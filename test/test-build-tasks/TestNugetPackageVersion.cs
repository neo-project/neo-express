// Copyright (C) 2015-2024 The Neo Project.
//
// TestNugetPackageVersion.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BuildTasks;
using Xunit;

namespace build_tasks
{
    public class TestNugetPackageVersion
    {
        [Fact]
        public void parse_simple_version()
        {
            Assert.True(NugetPackageVersion.TryParse("1.2.3", out var version));
            Assert.Equal(1, version.Major);
            Assert.Equal(2, version.Minor);
            Assert.Equal(3, version.Patch);
            Assert.Equal(string.Empty, version.Suffix);
        }

        [Fact]
        public void parse_dotnet_version_fails()
        {
            Assert.False(NugetPackageVersion.TryParse("1.2.3.4", out var version));
        }

        [Fact]
        public void parse_version_with_prerel()
        {
            Assert.True(NugetPackageVersion.TryParse("1.2.3-prerel", out var version));
            Assert.Equal(1, version.Major);
            Assert.Equal(2, version.Minor);
            Assert.Equal(3, version.Patch);
            Assert.Equal("prerel", version.Suffix);
        }

        [Fact]
        public void parse_version_with_build_id_fails()
        {
            Assert.False(NugetPackageVersion.TryParse("1.2.3+21AF26D3", out var version));
        }

        [Fact]
        public void parse_version_with_prerel_and_build_id()
        {
            Assert.True(NugetPackageVersion.TryParse("1.2.3-alpha+001", out var version));
            Assert.Equal(1, version.Major);
            Assert.Equal(2, version.Minor);
            Assert.Equal(3, version.Patch);
            Assert.Equal("alpha+001", version.Suffix);
        }

        [Fact]
        public void compare_major()
        {
            var one = new NugetPackageVersion(1, 0, 0);
            var two = new NugetPackageVersion(2, 0, 0);
            var result = one.CompareTo(two);
            Assert.True(result < 0);
        }

        [Fact]
        public void compare_minor()
        {
            var one = new NugetPackageVersion(1, 2, 0);
            var two = new NugetPackageVersion(1, 3, 0);
            var result = one.CompareTo(two);
            Assert.True(result < 0);
        }

        [Fact]
        public void compare_patch()
        {
            var one = new NugetPackageVersion(1, 2, 3);
            var two = new NugetPackageVersion(1, 2, 4);
            var result = one.CompareTo(two);
            Assert.True(result < 0);
        }

        [Fact]
        public void compare_no_suffix()
        {
            var one = new NugetPackageVersion(1, 2, 3);
            var two = new NugetPackageVersion(1, 2, 3);
            var result = one.CompareTo(two);
            Assert.True(result == 0);
        }

        [Fact]
        public void compare_suffix_to_no_suffix()
        {
            var one = new NugetPackageVersion(1, 2, 3, "prerel");
            var two = new NugetPackageVersion(1, 2, 3);
            var result = one.CompareTo(two);
            Assert.True(result < 0);
        }

        [Fact]
        public void compare_both_have_suffix()
        {
            var one = new NugetPackageVersion(1, 2, 3, "alpha");
            var two = new NugetPackageVersion(1, 2, 3, "beta");
            var result = one.CompareTo(two);
            Assert.True(result < 0);
        }




    }
}
