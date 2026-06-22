// Copyright (C) 2015-2026 The Neo Project.
//
// ContractCoverageCollector.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Collector.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Neo.Collector
{
    class ContractCoverageCollector
    {
        readonly string contractName;
        readonly NeoDebugInfo debugInfo;
        readonly Dictionary<int, uint> hitMap = new();
        readonly Dictionary<int, (uint branchCount, uint continueCount)> branchMap = new();
        IReadOnlyDictionary<int, Instruction> instructionMap = ImmutableDictionary<int, Instruction>.Empty;

        public Hash160 ScriptHash => debugInfo.Hash;
        public IReadOnlyDictionary<int, uint> HitMap => hitMap;
        public IReadOnlyDictionary<int, (uint branchCount, uint continueCount)> BranchMap => branchMap;

        public ContractCoverageCollector(string contractName, NeoDebugInfo debugInfo)
        {
            this.contractName = contractName;
            this.debugInfo = debugInfo;
        }

        public void RecordHit(int address)
        {
            var hitCount = hitMap.TryGetValue(address, out var value) ? value : 0;
            hitMap[address] = hitCount + 1;
        }

        public void RecordBranch(int address, int offsetAddress, int branchResult)
        {
            var (branchCount, continueCount) = branchMap.TryGetValue(address, out var value)
                ? value : (0, 0);
            branchMap[address] = branchResult == address
                ? (branchCount, continueCount + 1)
                : branchResult == offsetAddress
                    ? (branchCount + 1, continueCount)
                    : throw new FormatException($"Branch result {branchResult} did not equal {address} or {offsetAddress}");
        }

        public void RecordScript(IEnumerable<(int address, Instruction instruction)> instructions)
        {
            if (this.instructionMap.Count > 0)
            {
                throw new InvalidOperationException($"RecordScript already called for {contractName}");
            }

            var instructionMap = new SortedDictionary<int, Instruction>();
            foreach (var (address, instruction) in instructions)
            {
                instructionMap.Add(address, instruction);
            }
            this.instructionMap = instructionMap;
        }

        public ContractCoverage CollectCoverage()
        {
            return new(contractName, debugInfo, instructionMap, hitMap, branchMap);
        }
    }
}
