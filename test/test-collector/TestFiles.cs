// Copyright (C) 2015-2024 The Neo Project.
//
// TestFiles.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Collector;
using Neo.Collector.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace test_collector;

static class TestFiles
{
    public static void TrackTestContract(this CodeCoverageCollector @this, string contractName, string debugInfoFileName)
    {
        var debugInfo = GetResource(debugInfoFileName, stream =>
        {
            if (debugInfoFileName.EndsWith(NeoDebugInfo.NEF_DBG_NFO_EXTENSION))
            {
                return NeoDebugInfo.TryLoadCompressed(stream, out var debugInfo)
                    ? debugInfo : throw new Exception("NeoDebugInfo.TryLoadCompressed failed");
            }
            else if (debugInfoFileName.EndsWith(NeoDebugInfo.DEBUG_JSON_EXTENSION))
            {
                return NeoDebugInfo.Load(stream);
            }
            else
            {
                throw new Exception($"Invalid debug info file extension {debugInfoFileName}");
            }
        });
        @this.TrackContract(contractName, debugInfo);
    }

    public static void LoadTestOutput(this CodeCoverageCollector @this, string dirName)
    {
        foreach (var file in GetResourceNames(dirName))
        {
            using var stream = GetResourceStream(file);
            var ext = Path.GetExtension(file);
            switch (ext)
            {
                case CodeCoverageCollector.COVERAGE_FILE_EXT:
                    @this.LoadCoverage(stream);
                    break;
                case CodeCoverageCollector.SCRIPT_FILE_EXT:
                    {
                        var array = file.Split('.');
                        @this.LoadScript(Hash160.Parse(array[^2]), stream);
                    }
                    break;
                case CodeCoverageCollector.NEF_FILE_EXT:
                    {
                        var array = file.Split('.');
                        @this.LoadNef(Hash160.Parse(array[^2]), stream);
                    }
                    break;
                default:
                    break;
            }
        }
    }

    public static T GetResource<T>(string name, Func<Stream, T> convertFunc)
    {
        using var stream = GetResourceStream(name);
        return convertFunc(stream);
    }

    public static Stream GetResourceStream(string name)
    {
        var assembly = typeof(TestFiles).Assembly;
        var stream = assembly.GetManifestResourceStream(name);
        if (stream is not null)
        {
            return stream;
        }

        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
        stream = string.IsNullOrEmpty(resourceName) ? null : assembly.GetManifestResourceStream(resourceName);
        return stream ?? throw new FileNotFoundException();
    }

    public static IEnumerable<string> GetResourceNames(string dirName = "")
    {
        var assembly = typeof(TestFiles).Assembly;
        var names = assembly.GetManifestResourceNames();
        return string.IsNullOrEmpty(dirName)
            ? names
            : names.Where(n => n.Contains(dirName, StringComparison.OrdinalIgnoreCase));
    }
}
