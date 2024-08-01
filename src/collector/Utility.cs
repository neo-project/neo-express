// Copyright (C) 2015-2024 The Neo Project.
//
// Utility.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;

namespace Neo.Collector
{
    static class Utility
    {
        // Note, some file systems are case sensitive. 
        // Using StringComparison.OrdinalIgnoreCase could lead to incorrect base names on such systems. 
        public static string GetBaseName(string path, string suffix, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            path = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(suffix)
                && path.EndsWith(suffix, comparison))
            {
                return path.Substring(0, path.Length - suffix.Length);
            }
            return path;
        }

        public static bool TryLoadAssembly(string path, [MaybeNullWhen(false)] out Assembly assembly)
        {
            if (File.Exists(path))
            {
                try
                {
                    assembly = Assembly.LoadFrom(path);
                    return true;
                }
                catch { }
            }

            assembly = default;
            return false;
        }

        public static decimal CalculateHitRate(uint lineCount, uint hitCount)
            => lineCount == 0 ? 1m : new decimal(hitCount) / new decimal(lineCount);
    }
}
