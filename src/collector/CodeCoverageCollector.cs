// Copyright (C) 2015-2024 The Neo Project.
//
// CodeCoverageCollector.cs file belongs to neo-express project and is free
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
using System.Reflection;

namespace Neo.Collector
{
    // Testable version of CodeCoverageDataCollector
    class CodeCoverageCollector
    {
        internal const string TEST_HARNESS_NAMESPACE = "NeoTestHarness";
        internal const string CONTRACT_ATTRIBUTE_NAME = "ContractAttribute";
        internal const string COVERAGE_FILE_EXT = ".neo-coverage";
        internal const string SCRIPT_FILE_EXT = ".neo-script";
        internal const string NEF_FILE_EXT = ".nef";

        readonly ILogger logger;
        readonly bool verboseLog;
        readonly IDictionary<Hash160, ContractCoverageCollector> coverageMap = new Dictionary<Hash160, ContractCoverageCollector>();

        public IEnumerable<ContractCoverageCollector> ContractCollectors => coverageMap.Values;

        public CodeCoverageCollector(ILogger logger, bool verboseLog = false)
        {
            this.logger = logger;
            this.verboseLog = verboseLog;
        }

        public IEnumerable<ContractCoverage> CollectCoverage()
            => coverageMap.Values.Select(c => c.CollectCoverage());

        public void LoadDebugInfoSetting(string path, string name)
        {
            if (verboseLog)
                logger.LogWarning($"LoadDebugInfoSetting {path}");
            if (NeoDebugInfo.TryLoad(path, out var debugInfo))
            {
                if (string.IsNullOrEmpty(name))
                {
                    name = Path.GetFileNameWithoutExtension(path);
                }
                TrackContract(name, debugInfo);
            }
            else
            {
                logger.LogError($"LoadDebugInfoSetting failed to load {path}");
            }
        }

        public void LoadTestSource(string testSource)
        {
            if (verboseLog)
                logger.LogWarning($"LoadTestSource {testSource}");
            if (Utility.TryLoadAssembly(testSource, out var asm))
            {
                foreach (var type in asm.DefinedTypes)
                {
                    if (TryGetContractAttribute(type, out var contractName, out var manifestPath)
                        && NeoDebugInfo.TryLoadManifestDebugInfo(manifestPath, out var debugInfo))
                    {
                        TrackContract(contractName, debugInfo);
                    }
                }
            }
            else
            {
                logger.LogError($"LoadTestSource failed to load {testSource}");
            }
        }

        static bool TryGetContractAttribute(TypeInfo type, out string name, out string manifestPath)
        {
            if (type.IsInterface)
            {
                foreach (var a in type.GetCustomAttributesData())
                {
                    if (a.AttributeType.Name == CONTRACT_ATTRIBUTE_NAME && a.AttributeType.Namespace == TEST_HARNESS_NAMESPACE)
                    {
                        name = (string)a.ConstructorArguments[0].Value;
                        manifestPath = (string)a.ConstructorArguments[1].Value;
                        return true;
                    }
                }
            }

            name = "";
            manifestPath = "";
            return false;
        }

        public void TrackContract(string contractName, NeoDebugInfo debugInfo)
        {
            if (verboseLog)
                logger.LogWarning($"TrackContract {contractName}");
            if (!coverageMap.ContainsKey(debugInfo.Hash))
            {
                coverageMap.Add(debugInfo.Hash, new ContractCoverageCollector(contractName, debugInfo));
            }
        }

        public void LoadCoverageFiles(string coveragePath)
        {
            foreach (var filename in Directory.EnumerateFiles(coveragePath))
            {
                if (verboseLog)
                    logger.LogWarning($"LoadCoverageFiles {filename}");
                try
                {
                    var ext = Path.GetExtension(filename);
                    switch (ext)
                    {
                        case COVERAGE_FILE_EXT:
                            LoadCoverage(filename);
                            break;
                        case NEF_FILE_EXT:
                            LoadNef(filename);
                            break;
                        case SCRIPT_FILE_EXT:
                            LoadScript(filename);
                            break;
                        default:
                            logger.LogWarning($"Unrecognized coverage output extension {filename}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(filename, ex);
                }
            }
        }

        public void LoadCoverage(string filename)
        {
            using (var stream = File.OpenRead(filename))
            {
                LoadCoverage(stream);
            }
        }

        // Stream based methods broken out for test purposes
        internal void LoadCoverage(Stream stream)
        {
            var reader = new StreamReader(stream);
            var hash = Hash160.Zero;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line.StartsWith("0x"))
                {
                    hash = Hash160.TryParse(line.Trim(), out var value)
                        ? value
                        : Hash160.Zero;
                }
                else
                {
                    if (hash != Hash160.Zero
                        && coverageMap.TryGetValue(hash, out var coverage))
                    {
                        var values = line.Trim().Split(' ');
                        if (values.Length > 0
                            && int.TryParse(values[0].Trim(), out var ip))
                        {
                            if (values.Length == 1)
                            {
                                coverage.RecordHit(ip);
                            }
                            else if (values.Length == 3
                                && int.TryParse(values[1].Trim(), out var offset)
                                && int.TryParse(values[2].Trim(), out var branchResult))
                            {
                                coverage.RecordBranch(ip, offset, branchResult);
                            }
                            else
                            {
                                logger.LogError($"Invalid raw coverage data line '{line}'");
                            }
                        }
                    }
                }
            }
        }

        public void LoadScript(string filename)
        {
            var baseFileName = Path.GetFileNameWithoutExtension(filename);
            if (Hash160.TryParse(baseFileName, out var hash))
            {
                using (var stream = File.OpenRead(filename))
                {
                    LoadScript(hash, stream);
                }
            }
            else
            {
                logger.LogError($"Failed to parse {baseFileName} filename");
            }
        }

        internal void LoadScript(Hash160 hash, Stream stream)
        {
            if (coverageMap.TryGetValue(hash, out var coverage))
            {
                coverage.RecordScript(stream.EnumerateInstructions());
            }
            else
            {
                if (verboseLog)
                    logger.LogWarning($"{hash} script not tracked");
            }
        }

        public void LoadNef(string filename)
        {
            var baseFileName = Path.GetFileNameWithoutExtension(filename);
            if (Hash160.TryParse(baseFileName, out var hash))
            {
                using (var stream = File.OpenRead(filename))
                {
                    LoadNef(hash, stream);
                }
            }
            else
            {
                logger.LogError($"Failed to parse {baseFileName} filename");
            }
        }

        internal void LoadNef(Hash160 hash, Stream stream)
        {
            if (coverageMap.TryGetValue(hash, out var coverage))
            {
                var nefFile = NefFile.Load(stream);
                coverage.RecordScript(nefFile.Script.EnumerateInstructions());
            }
            else
            {
                if (verboseLog)
                    logger.LogWarning($"{hash} nef not tracked");
            }
        }
    }
}
