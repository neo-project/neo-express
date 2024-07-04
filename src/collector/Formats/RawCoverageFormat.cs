// Copyright (C) 2015-2024 The Neo Project.
//
// RawCoverageFormat.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
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
    class RawCoverageFormat : ICoverageFormat
    {
        public void WriteReport(IReadOnlyList<ContractCoverage> coverage, Action<string, Action<Stream>> writeAttachement)
        {
            foreach (var contract in coverage)
            {
                var filename = $"{contract.DebugInfo.Hash}.neo-coverage.txt";
                writeAttachement(filename, stream =>
                {
                    var writer = new StreamWriter(stream);
                    var addresses = contract.InstructionMap.Select(kvp => kvp.Key).OrderBy(h => h);
                    foreach (var address in addresses)
                    {
                        if (contract.BranchHitMap.TryGetValue(address, out var branchHits))
                        {
                            writer.WriteLine($"{address} {branchHits.BranchCount} {branchHits.ContinueCount}");
                        }
                        else if (contract.HitMap.TryGetValue(address, out var count))
                        {
                            writer.WriteLine($"{address} {count}");
                        }
                        else
                        {
                            writer.WriteLine($"{address} 0");
                        }
                    }
                    writer.Flush();
                });
            }
        }
    }
}
