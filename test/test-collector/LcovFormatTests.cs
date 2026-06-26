// Copyright (C) 2015-2026 The Neo Project.
//
// LcovFormatTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Moq;
using Neo.Collector;
using Neo.Collector.Formats;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace test_collector;

public class LcovFormatTests
{
    static string WriteLcov()
    {
        var logger = new Mock<ILogger>();
        var collector = new CodeCoverageCollector(logger.Object);
        collector.TrackTestContract("contract", "registrar.nefdbgnfo");
        collector.LoadTestOutput("run1");
        var coverage = collector.CollectCoverage().ToList();

        var lcov = "";
        new LcovFormat().WriteReport(coverage, (filename, write) =>
        {
            Assert.EndsWith(".lcov.info", filename);
            using var ms = new MemoryStream();
            write(ms);
            lcov = Encoding.UTF8.GetString(ms.ToArray());
        });
        return lcov;
    }

    [Fact]
    public void Produces_a_well_formed_lcov_record()
    {
        var lcov = WriteLcov();

        Assert.Contains("SF:", lcov);
        Assert.Contains("DA:", lcov);
        Assert.Contains("end_of_record", lcov);

        // every record is terminated
        var sf = lcov.Split('\n').Count(l => l.StartsWith("SF:"));
        var eor = lcov.Split('\n').Count(l => l.Trim() == "end_of_record");
        Assert.Equal(sf, eor);
    }

    [Fact]
    public void Lines_hit_never_exceeds_lines_found()
    {
        var lcov = WriteLcov();

        foreach (var line in lcov.Split('\n'))
        {
            if (line.StartsWith("LF:"))
            {
                var lf = int.Parse(line.Substring(3).Trim());
                Assert.True(lf >= 0);
            }
            if (line.StartsWith("LH:"))
            {
                var lh = int.Parse(line.Substring(3).Trim());
                Assert.True(lh >= 0);
            }
        }

        // pair up LF/LH per record and assert LH <= LF
        var records = lcov.Split(new[] { "end_of_record" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var record in records)
        {
            if (!record.Contains("LF:"))
                continue;
            var lf = ReadCounter(record, "LF:");
            var lh = ReadCounter(record, "LH:");
            Assert.True(lh <= lf, $"LH ({lh}) must not exceed LF ({lf})");
        }
    }

    static int ReadCounter(string record, string prefix)
    {
        var line = record.Split('\n').Single(l => l.StartsWith(prefix));
        return int.Parse(line.Substring(prefix.Length).Trim());
    }
}
