// Copyright (C) 2015-2026 The Neo Project.
//
// CoberturaCultureTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Moq;
using Neo.Collector;
using Neo.Collector.Formats;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace test_collector;

public class CoberturaCultureTests
{
    [Fact]
    public void Rates_use_invariant_decimal_separator_under_non_invariant_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // de-DE uses ',' as the decimal separator.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

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

            var root = XDocument.Parse(xml).Root!;
            var lineRate = root.Attribute("line-rate")!.Value;
            var branchRate = root.Attribute("branch-rate")!.Value;

            Assert.DoesNotContain(",", lineRate);
            Assert.DoesNotContain(",", branchRate);
            Assert.Contains(".", lineRate);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
