// Copyright (C) 2015-2024 The Neo Project.
//
// UnitTest1.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Collector;
using Neo.Collector.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace test_collector;

public class UnitTest1
{
    [Fact]
    public void test_generate_coverage()
    {
        var logger = new Moq.Mock<ILogger>();
        var collector = new CodeCoverageCollector(logger.Object);
        collector.TrackTestContract("contract", "registrar.nefdbgnfo");
        collector.LoadTestOutput("run1");
        var coverage = collector.CollectCoverage().ToList();

        var format = new CoberturaFormat();
        Dictionary<string, string> outputMap = new();

        format.WriteReport(coverage, writeAttachment);

        void writeAttachment(string filename, Action<Stream> writeAttachment)
        {
            using var stream = new MemoryStream();
            var text = Encoding.UTF8.GetString(stream.ToArray());
            outputMap.Add(filename, text);
        }
    }
}
