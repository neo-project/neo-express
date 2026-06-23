// Copyright (C) 2015-2026 The Neo Project.
//
// CoberturaBranchRateTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Moq;
using Neo.Collector;
using Neo.Collector.Formats;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace test_collector;

public class CoberturaBranchRateTests
{
    static string RenderReport()
    {
        var collector = new CodeCoverageCollector(new Mock<ILogger>().Object);
        collector.TrackTestContract("contract", "registrar.nefdbgnfo");
        collector.LoadTestOutput("run1");
        var coverage = collector.CollectCoverage().ToList();

        var xml = "";
        new CoberturaFormat().WriteReport(coverage, (filename, write) =>
        {
            using var ms = new MemoryStream();
            write(ms);
            xml = Encoding.UTF8.GetString(ms.ToArray());
        });
        return xml;
    }

    // The branch rate and per-condition rendering are produced from a single walk of the
    // branch instructions; these assertions pin that output so the optimization stays
    // behavior-preserving.
    [Fact]
    public void Branch_rate_and_conditions_are_rendered()
    {
        var xml = RenderReport();

        Assert.Contains("branches-valid=\"28\"", xml);
        Assert.Contains("branches-covered=\"10\"", xml);
        Assert.Contains("branch=\"true\"", xml);
        Assert.Contains("condition-coverage=\"83.333% (5/6)\"", xml);
        Assert.Contains("<conditions>", xml);
        Assert.Contains("<condition ", xml);
    }
}
