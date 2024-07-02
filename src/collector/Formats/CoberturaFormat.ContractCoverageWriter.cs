// Copyright (C) 2015-2024 The Neo Project.
//
// CoberturaFormat.ContractCoverageWriter.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Collector.Models;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Neo.Collector.Formats
{
    partial class CoberturaFormat
    {
        internal class ContractCoverageWriter
        {
            readonly ContractCoverage contract;

            NeoDebugInfo DebugInfo => contract.DebugInfo;

            public ContractCoverageWriter(ContractCoverage contract)
            {
                this.contract = contract;
            }

            bool GetAddressHit(int address) => contract.HitMap.TryGetValue(address, out var count) && count > 0;
            (uint, uint) GetBranchHit(int address) => contract.BranchHitMap.TryGetValue(address, out var value) ? value : (0, 0);

            public void WritePackage(XmlWriter writer)
            {
                var lineRate = DebugInfo.Methods.SelectMany(m => m.SequencePoints).CalculateLineRate(GetAddressHit);
                var branchRate = contract.InstructionMap.CalculateBranchRate(DebugInfo.Methods, GetBranchHit);

                writer.WriteStartElement("package");
                // TODO: complexity
                writer.WriteAttributeString("name", contract.Name);
                writer.WriteAttributeString("scripthash", $"{contract.DebugInfo.Hash}");
                writer.WriteAttributeString("line-rate", $"{lineRate:N4}");
                writer.WriteAttributeString("branch-rate", $"{branchRate:N4}");
                writer.WriteStartElement("classes");
                {
                    foreach (var group in DebugInfo.Methods.GroupBy(NamespaceAndFilename))
                    {
                        WriteClass(writer, group.Key.@namespace, group.Key.filename, group);
                    }
                }
                writer.WriteEndElement();
                writer.WriteEndElement();

                (string @namespace, string filename) NamespaceAndFilename(NeoDebugInfo.Method method)
                {
                    var indexes = method.SequencePoints
                        .Select(sp => sp.Document)
                        .Distinct()
                        .ToList();
                    if (indexes.Count == 1)
                    {
                        var index = indexes[0];
                        if (index >= 0 && index < DebugInfo.Documents.Count)
                        {
                            return (method.Namespace, DebugInfo.Documents[index]);
                        }
                    }
                    return (method.Namespace, string.Empty);
                }
            }
            internal void WriteClass(XmlWriter writer, string name, string filename, IEnumerable<NeoDebugInfo.Method> methods)
            {
                var lineRate = methods.SelectMany(m => m.SequencePoints).CalculateLineRate(GetAddressHit);
                var branchRate = contract.InstructionMap.CalculateBranchRate(methods, GetBranchHit);

                writer.WriteStartElement("class");
                // TODO: complexity
                writer.WriteAttributeString("name", name);
                if (filename.Length > 0)
                { writer.WriteAttributeString("filename", filename); }
                writer.WriteAttributeString("line-rate", $"{lineRate:N4}");
                writer.WriteAttributeString("branch-rate", $"{branchRate:N4}");

                writer.WriteStartElement("methods");
                foreach (var method in methods)
                {
                    WriteMethod(writer, method);
                }
                writer.WriteEndElement();

                writer.WriteStartElement("lines");
                foreach (var method in methods)
                {
                    for (int i = 0; i < method.SequencePoints.Count; i++)
                    {
                        WriteLine(writer, method, i);
                    }
                }
                writer.WriteEndElement();

                writer.WriteEndElement();
            }

            internal void WriteMethod(XmlWriter writer, NeoDebugInfo.Method method)
            {
                var signature = string.Join(", ", method.Parameters.Select(p => p.Type));
                var lineRate = method.SequencePoints.CalculateLineRate(GetAddressHit);
                var branchRate = contract.InstructionMap.CalculateBranchRate(method, GetBranchHit);

                writer.WriteStartElement("method");
                writer.WriteAttributeString("name", method.Name);
                writer.WriteAttributeString("signature", $"({signature})");
                writer.WriteAttributeString("line-rate", $"{lineRate:N4}");
                writer.WriteAttributeString("branch-rate", $"{branchRate:N4}");
                writer.WriteStartElement("lines");
                for (int i = 0; i < method.SequencePoints.Count; i++)
                {
                    WriteLine(writer, method, i);
                }
                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            internal void WriteLine(XmlWriter writer, NeoDebugInfo.Method method, int index)
            {
                var sp = method.SequencePoints[index];
                var hits = contract.HitMap.TryGetValue(sp.Address, out var value) ? value : 0;
                var (branchCount, branchHit) = contract.InstructionMap.GetBranchRate(method, index, GetBranchHit);

                writer.WriteStartElement("line");
                writer.WriteAttributeString("number", $"{sp.Start.Line}");
                writer.WriteAttributeString("address", $"{sp.Address}");
                writer.WriteAttributeString("hits", $"{hits}");

                if (branchCount == 0)
                {
                    writer.WriteAttributeString("branch", $"{false}");
                }
                else
                {
                    var branchRate = Utility.CalculateHitRate(branchCount, branchHit);

                    writer.WriteAttributeString("branch", $"{true}");
                    writer.WriteAttributeString("condition-coverage", $"{branchRate * 100:N}% ({branchHit}/{branchCount})");
                    writer.WriteStartElement("conditions");
                    foreach (var (address, opCode) in contract.InstructionMap.GetBranchInstructions(method, index))
                    {
                        var (condBranchCount, condContinueCount) = GetBranchHit(address);
                        var coverage = condBranchCount == 0 ? 0m : 1m;
                        coverage += condContinueCount == 0 ? 0m : 1m;

                        writer.WriteStartElement("condition");
                        writer.WriteAttributeString("number", $"{address}");
                        writer.WriteAttributeString("type", $"{opCode}");
                        writer.WriteAttributeString("coverage", $"{coverage / 2m * 100m}%");
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
        }
    }
}
