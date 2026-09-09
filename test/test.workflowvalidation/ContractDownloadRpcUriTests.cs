// Copyright (C) 2015-2026 The Neo Project.
//
// ContractDownloadRpcUriTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress.Commands;
using Xunit;

namespace test.workflowvalidation;

public class ContractDownloadRpcUriTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeRpcUri_defaults_empty_to_mainnet(string? value)
    {
        ContractCommand.Download.NormalizeRpcUri(value!).Should().Be("mainnet");
    }

    [Fact]
    public void NormalizeRpcUri_keeps_an_explicit_uri()
    {
        ContractCommand.Download.NormalizeRpcUri("testnet").Should().Be("testnet");
        ContractCommand.Download.NormalizeRpcUri("http://localhost:10332").Should().Be("http://localhost:10332");
    }
}
