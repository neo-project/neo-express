// Copyright (C) 2015-2026 The Neo Project.
//
// NugetPackageVersion.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;

namespace Neo.BuildTasks
{
    // https://docs.microsoft.com/en-us/nuget/concepts/package-versioning#version-basics
    public readonly struct NugetPackageVersion : IComparable<NugetPackageVersion>
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Patch;
        public readonly string Suffix;

        public NugetPackageVersion(int major, int minor, int patch, string suffix = "")
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Suffix = suffix;
        }

        public override string ToString()
        {
            var core = $"{Major}.{Minor}.{Patch}";
            return Suffix.Length == 0 ? core : core + $"-{Suffix}";
        }

        public static bool TryParse(string input, out NugetPackageVersion version)
        {
            version = default;

            int majorStart = 0;
            int majorEnd = input.IndexOf('.');
            if (majorEnd < 0)
                return false;
            var majorText = input.Substring(0, majorEnd - majorStart);
            if (!int.TryParse(majorText, System.Globalization.NumberStyles.Integer, null, out var major))
                return false;

            var minorStart = majorEnd + 1;
            var minorEnd = input.IndexOf('.', minorStart);
            if (minorEnd < 0)
                return false;
            var minorText = input.Substring(minorStart, minorEnd - minorStart);
            if (!int.TryParse(minorText, System.Globalization.NumberStyles.Integer, null, out var minor))
                return false;

            var patchStart = minorEnd + 1;
            var patchEnd = input.IndexOf('-', patchStart);
            if (patchEnd < 0)
            {
                var patchText = input.Substring(patchStart);
                if (!int.TryParse(patchText, System.Globalization.NumberStyles.Integer, null, out var patch))
                    return false;

                version = new NugetPackageVersion(major, minor, patch);
                return true;
            }
            else
            {
                var patchText = input.Substring(patchStart, patchEnd - patchStart);
                if (!int.TryParse(patchText, System.Globalization.NumberStyles.Integer, null, out var patch))
                    return false;
                var suffix = input.Substring(patchEnd + 1);
                version = new NugetPackageVersion(major, minor, patch, suffix);
                return true;
            }
        }

        public int CompareTo(NugetPackageVersion other)
        {
            var result = Major.CompareTo(other.Major);
            if (result != 0)
                return result;

            result = Minor.CompareTo(other.Minor);
            if (result != 0)
                return result;

            result = Patch.CompareTo(other.Patch);
            if (result != 0)
                return result;

            // Suffix is null for default(NugetPackageVersion); treat it as empty.
            var suffix = Suffix ?? string.Empty;
            var otherSuffix = other.Suffix ?? string.Empty;
            if (suffix.Length == 0 && otherSuffix.Length > 0)
                return 1;
            if (suffix.Length > 0 && otherSuffix.Length == 0)
                return -1;
            return string.Compare(suffix, otherSuffix, true);
        }

        public override bool Equals(object? obj)
        {
            // Defer to CompareTo so equality agrees with the == operator, including its
            // null-suffix normalization and case-insensitive suffix comparison.
            return obj is NugetPackageVersion version && CompareTo(version) == 0;
        }

        public override int GetHashCode()
        {
            int hashCode = -261206211;
            hashCode = hashCode * -1521134295 + Major.GetHashCode();
            hashCode = hashCode * -1521134295 + Minor.GetHashCode();
            hashCode = hashCode * -1521134295 + Patch.GetHashCode();
            // Match Equals/CompareTo: a null suffix hashes as empty and case is ignored.
            hashCode = hashCode * -1521134295 + StringComparer.OrdinalIgnoreCase.GetHashCode(Suffix ?? string.Empty);
            return hashCode;
        }

        public static bool operator ==(in NugetPackageVersion left, in NugetPackageVersion right)
        {
            return left.CompareTo(right) == 0;
        }

        public static bool operator !=(in NugetPackageVersion left, in NugetPackageVersion right)
        {
            return left.CompareTo(right) != 0;
        }

        public static bool operator >(in NugetPackageVersion left, in NugetPackageVersion right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(in NugetPackageVersion left, in NugetPackageVersion right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator <(in NugetPackageVersion left, in NugetPackageVersion right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(in NugetPackageVersion left, in NugetPackageVersion right)
        {
            return left.CompareTo(right) <= 0;
        }
    }
}
