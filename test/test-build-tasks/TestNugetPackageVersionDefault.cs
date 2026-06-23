// Copyright (C) 2015-2026 The Neo Project.
//
// TestNugetPackageVersionDefault.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BuildTasks;
using Xunit;

namespace build_tasks
{
    public class TestNugetPackageVersionDefault
    {
        [Fact]
        public void compare_against_default_does_not_throw()
        {
            var def = default(NugetPackageVersion); // Suffix is null
            var zero = new NugetPackageVersion(0, 0, 0); // Suffix is ""

            Assert.Equal(0, zero.CompareTo(def));
            Assert.Equal(0, def.CompareTo(zero));
            Assert.Equal(0, def.CompareTo(def));
            Assert.True(def == def);
        }

        [Fact]
        public void equals_and_gethashcode_agree_with_the_equality_operator_for_default()
        {
            var def = default(NugetPackageVersion); // Suffix is null
            var zero = new NugetPackageVersion(0, 0, 0); // Suffix is ""

            Assert.True(def == zero);
            Assert.True(def.Equals(zero));        // must agree with ==
            Assert.True(zero.Equals(def));
            Assert.Equal(zero.GetHashCode(), def.GetHashCode());
            Assert.Equal(0, def.GetHashCode() - def.GetHashCode()); // does not throw
        }

        [Fact]
        public void equals_and_gethashcode_ignore_suffix_case_like_compareto()
        {
            var upper = new NugetPackageVersion(1, 2, 3, "RC1");
            var lower = new NugetPackageVersion(1, 2, 3, "rc1");

            Assert.True(upper == lower);
            Assert.True(upper.Equals(lower));     // must agree with ==
            Assert.Equal(upper.GetHashCode(), lower.GetHashCode());
        }
    }
}
