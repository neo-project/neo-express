// Copyright (C) 2015-2026 The Neo Project.
//
// ContractStorageUpdateCommandTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using NeoExpress.Commands;
using System.Text;
using Xunit;

namespace test.workflowvalidation;

public class ContractStorageUpdateCommandTests
{
    [Fact]
    public void ConvertStorageArgument_PreservesUnicodeTextAsUtf8()
    {
        var parser = new ContractParameterParser(ProtocolSettings.Default);
        const string value = "neo-汉字";

        var result = ContractCommand.Storage.StorageUpdateKeyValue.ConvertStorageArgument(parser, value);

        result.Should().Be(Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
    }

    [Fact]
    public void ConvertStorageArgument_PreservesBase64Input()
    {
        var parser = new ContractParameterParser(ProtocolSettings.Default);
        var value = Convert.ToBase64String([0xde, 0xad, 0xbe, 0xef]);

        var result = ContractCommand.Storage.StorageUpdateKeyValue.ConvertStorageArgument(parser, value);

        result.Should().Be(value);
    }
}
