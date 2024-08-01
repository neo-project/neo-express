// Copyright (C) 2015-2024 The Neo Project.
//
// CodeCoverageTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Xunit;

namespace test.bctklib
{
    public class CodeCoverageTests : IClassFixture<DeployedContractFixture>
    {
        const string COVERAGE_ENV_NAME = "NEO_TEST_APP_ENGINE_COVERAGE_PATH";

        readonly DeployedContractFixture deployedContractFixture;
        readonly Script script;

        public CodeCoverageTests(DeployedContractFixture deployedContractFixture)
        {
            this.deployedContractFixture = deployedContractFixture;

            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(deployedContractFixture.ContractHash, "query", CallFlags.All, "test.domain");
            script = builder.ToArray();
        }

        [Fact]
        public void Coverage_Generated()
        {
            var fs = new MockFileSystem();
            var coveragePath = fs.Path.Combine(fs.Path.GetTempPath(), fs.Path.GetRandomFileName());
            System.Environment.SetEnvironmentVariable(COVERAGE_ENV_NAME, coveragePath);

            using var snapshot = new SnapshotCache(deployedContractFixture.Store);
            using var engine = new TestApplicationEngine(snapshot, fileSystem: fs);

            engine.LoadScript(script);
            var result = engine.Execute();

            var scriptPath = fs.Path.Combine(coveragePath, "0xb127f874f7c5a287148f9b35baa43f147f271dba.neo-script");
            fs.FileExists(scriptPath).Should().BeTrue();

            fs.AllFiles
                .Where(f => fs.Path.GetExtension(f) == ".neo-coverage")
                .Count().Should().Be(1);
        }
    }
}
