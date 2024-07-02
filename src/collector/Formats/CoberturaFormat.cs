// Copyright (C) 2015-2024 The Neo Project.
//
// CoberturaFormat.cs file belongs to neo-express project and is free
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
using System.Xml;

namespace Neo.Collector.Formats
{
    partial class CoberturaFormat : ICoverageFormat
    {
        public void WriteReport(IReadOnlyList<ContractCoverage> coverage, Action<string, Action<Stream>> writeAttachement)
        {
            writeAttachement("neo-coverage.cobertura.xml", stream =>
            {
                var textWriter = new StreamWriter(stream);
                var xmlWriter = new XmlTextWriter(textWriter) { Formatting = Formatting.Indented };
                WriteReport(xmlWriter, coverage);
                xmlWriter.Flush();
                textWriter.Flush();
            });
        }

        internal void WriteReport(XmlWriter writer, IReadOnlyList<ContractCoverage> coverage)
        {
            uint linesValid = 0u, linesCovered = 0u, branchesValid = 0, branchesCovered = 0;
            foreach (var contract in coverage)
            {
                bool hitFunc(int address) => contract.HitMap.TryGetValue(address, out var count) && count > 0;
                var (lineCount, hitCount) = contract.DebugInfo.Methods.SelectMany(m => m.SequencePoints).GetLineRate(hitFunc);
                linesValid += lineCount;
                linesCovered += hitCount;

                (uint, uint) branchHitFunc(int address) => contract.BranchHitMap.TryGetValue(address, out var value) ? value : (0, 0);
                var (branchCount, branchHit) = contract.InstructionMap.GetBranchRate(contract.DebugInfo.Methods, branchHitFunc);
                branchesValid += branchCount;
                branchesCovered += branchHit;
            }
            var lineRate = Utility.CalculateHitRate(linesValid, linesCovered);
            var branchRate = Utility.CalculateHitRate(branchesValid, branchesCovered);

            writer.WriteStartDocument();
            writer.WriteStartElement("coverage");
            writer.WriteAttributeString("line-rate", $"{lineRate:N4}");
            writer.WriteAttributeString("lines-covered", $"{linesCovered}");
            writer.WriteAttributeString("lines-valid", $"{linesValid}");
            writer.WriteAttributeString("branch-rate", $"{branchRate:N4}");
            writer.WriteAttributeString("branches-covered", $"{branchesCovered}");
            writer.WriteAttributeString("branches-valid", $"{branchesValid}");
            writer.WriteAttributeString("version", ThisAssembly.AssemblyFileVersion);
            writer.WriteAttributeString("timestamp", $"{DateTimeOffset.Now.ToUnixTimeSeconds()}");

            writer.WriteStartElement("sources");
            foreach (var contract in coverage)
            {
                writer.WriteElementString("source", contract.DebugInfo.DocumentRoot);
            }
            writer.WriteEndElement();

            writer.WriteStartElement("packages");
            foreach (var contract in coverage)
            {
                var ccWriter = new ContractCoverageWriter(contract);
                ccWriter.WritePackage(writer);
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
    }
}
