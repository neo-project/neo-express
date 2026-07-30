// Copyright (C) 2015-2026 The Neo Project.
//
// Neo310SettingsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit.Models;
using NeoExpress;
using System.Xml.Linq;
using Xunit;

namespace test.workflowvalidation;

public class Neo310SettingsTests
{
    [Fact]
    public void Neo_package_versions_are_aligned_to_3101()
    {
        var propsPath = FindRepoFile("src", "Directory.Build.props");
        var props = XDocument.Load(propsPath);
        XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

        props.Descendants(ns + "NeoVersion").Single().Value.Should().Be("3.10.1");
        props.Descendants(ns + "NeoModuleVersion").Single().Value.Should().Be("3.10.1");
        props.Descendants(ns + "NeoConsensusDbftVersion").Single().Value.Should().Be("3.10.1");
    }

    [Fact]
    public void Dbft_settings_match_neo_node_310_manual_start_defaults()
    {
        var chain = ExpressChainManagerFactory.CreateChain(1, null);
        var settings = ExpressChainManager.CreateConsensusSettings(chain);

        settings.AutoStart.Should().BeFalse();
        settings.IgnoreRecoveryLogs.Should().BeTrue();
        settings.MaxBlockSystemFee.Should().Be(2_000_000_000L);
    }

    [Fact]
    public void Rpc_settings_allow_request_body_size_override()
    {
        var chain = ExpressChainManagerFactory.CreateChain(1, null);
        chain.Settings["rpc.MaxRequestBodySize"] = "2097152";

        var settings = ExpressChainManager.CreateRpcServerSettings(
            chain,
            chain.ConsensusNodes[0]);

        settings.MaxRequestBodySize.Should().Be(2_097_152);
    }

    private static string FindRepoFile(params string[] pathParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine([current.FullName, .. pathParts]);
            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(pathParts)} from {AppContext.BaseDirectory}");
    }
}
