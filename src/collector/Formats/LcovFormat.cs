// Copyright (C) 2015-2026 The Neo Project.
//
// LcovFormat.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Collector.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Collector.Formats
{
    // Emits line and function coverage in the LCOV tracefile format consumed by
    // genhtml, Coveralls, Codecov and many editor coverage gutters. Branch coverage
    // is intentionally omitted here; it is available in the Cobertura report.
    class LcovFormat : ICoverageFormat
    {
        public void WriteReport(IReadOnlyList<ContractCoverage> coverage, Action<string, Action<Stream>> writeAttachement)
        {
            writeAttachement("neo-coverage.lcov.info", stream =>
            {
                var writer = new StreamWriter(stream);
                foreach (var contract in coverage)
                {
                    WriteContract(writer, contract);
                }
                writer.Flush();
            });
        }

        internal static void WriteContract(TextWriter writer, ContractCoverage contract)
        {
            var debugInfo = contract.DebugInfo;
            uint HitsAt(int address) => contract.HitMap.TryGetValue(address, out var count) ? count : 0u;

            var byDocument = debugInfo.Methods
                .Select(method => (method, document: SingleDocument(debugInfo, method)))
                .Where(x => x.document is not null)
                .GroupBy(x => x.document!, x => x.method);

            foreach (var group in byDocument)
            {
                writer.WriteLine("TN:");
                writer.WriteLine($"SF:{group.Key}");

                var methods = group.Where(m => m.SequencePoints.Count > 0).ToList();
                var fnHit = 0;
                foreach (var method in methods)
                {
                    writer.WriteLine($"FN:{method.SequencePoints[0].Start.Line},{method.Name}");
                }
                foreach (var method in methods)
                {
                    var hits = HitsAt(method.SequencePoints[0].Address);
                    if (hits > 0)
                        fnHit++;
                    writer.WriteLine($"FNDA:{hits},{method.Name}");
                }
                writer.WriteLine($"FNF:{methods.Count}");
                writer.WriteLine($"FNH:{fnHit}");

                // Aggregate per source line; a line can map to several sequence points,
                // so record the highest hit count observed for that line.
                var lineHits = new SortedDictionary<int, uint>();
                foreach (var method in group)
                {
                    foreach (var sp in method.SequencePoints)
                    {
                        var hits = HitsAt(sp.Address);
                        if (!lineHits.TryGetValue(sp.Start.Line, out var existing) || hits > existing)
                            lineHits[sp.Start.Line] = hits;
                    }
                }

                var linesHit = 0;
                foreach (var line in lineHits)
                {
                    writer.WriteLine($"DA:{line.Key},{line.Value}");
                    if (line.Value > 0)
                        linesHit++;
                }
                writer.WriteLine($"LF:{lineHits.Count}");
                writer.WriteLine($"LH:{linesHit}");
                writer.WriteLine("end_of_record");
            }
        }

        static string? SingleDocument(NeoDebugInfo debugInfo, NeoDebugInfo.Method method)
        {
            var documents = method.SequencePoints.Select(sp => sp.Document).Distinct().ToList();
            if (documents.Count == 1)
            {
                var index = documents[0];
                if (index >= 0 && index < debugInfo.Documents.Count)
                    return debugInfo.Documents[index];
            }
            return null;
        }
    }
}
