// Copyright (C) 2015-2026 The Neo Project.
//
// CoberturaBranchBoolTests.cs file belongs to neo-express project and is free
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

public class CoberturaBranchBoolTests
{
    [Fact]
    public void Branch_attribute_uses_lowercase_boolean()
    {
        var logger = new Mock<ILogger>();
        var collector = new CodeCoverageCollector(logger.Object);
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

        Assert.DoesNotContain("branch=\"True\"", xml);
        Assert.DoesNotContain("branch=\"False\"", xml);
        Assert.Contains("branch=\"false\"", xml);
    }
}
