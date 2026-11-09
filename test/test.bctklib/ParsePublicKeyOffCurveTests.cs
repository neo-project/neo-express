// Copyright (C) 2015-2026 The Neo Project.
//
// ParsePublicKeyOffCurveTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Newtonsoft.Json.Linq;
using System;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace test.bctklib
{
    public class ParsePublicKeyOffCurveTests
    {
        readonly byte DEFAULT_ADDRESS_VERSION = Neo.ProtocolSettings.Default.AddressVersion;

        // A syntactically valid 33-byte compressed point whose X coordinate is not on
        // secp256r1: the generator X with a single nibble mutated (e12c4247 -> e17c4247).
        // ECPoint.Parse delegates to ECPoint.DecompressPoint, which reports such a point
        // as ArithmeticException rather than ArgumentException or FormatException.
        const string OFF_CURVE_PREFIX_02 = "026b17d1f2e17c4247f8bce6e563a440f277037d812deb33a0f4a13945d898c296";
        const string OFF_CURVE_PREFIX_03 = "036b17d1f2e17c4247f8bce6e563a440f277037d812deb33a0f4a13945d898c296";
        const string ON_CURVE_GENERATOR = "026b17d1f2e12c4247f8bce6e563a440f277037d812deb33a0f4a13945d898c296";

        [Fact]
        public void ECPoint_Parse_reports_an_off_curve_point_as_ArithmeticException()
        {
            // Pins the assumption the parser fix relies on.
            var action = () => ECPoint.Parse(OFF_CURVE_PREFIX_02, ECCurve.Secp256r1);

            action.Should().Throw<ArithmeticException>();
        }

        [Theory]
        [InlineData(OFF_CURVE_PREFIX_02)]
        [InlineData(OFF_CURVE_PREFIX_03)]
        public void ParseObjectParameter_publickey_off_curve_reports_format_exception(string value)
        {
            var json = new JObject()
            {
                ["type"] = "PublicKey",
                ["value"] = value
            };
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);

            var action = () => parser.ParseObjectParameter(json);

            var exception = action.Should().Throw<FormatException>()
                .WithMessage("*Invalid PublicKey parameter value*");
            exception.Which.InnerException.Should().BeOfType<ArithmeticException>();
        }

        [Fact]
        public void ParseObjectParameter_publickey_on_curve_still_parses()
        {
            var json = new JObject()
            {
                ["type"] = "PublicKey",
                ["value"] = ON_CURVE_GENERATOR
            };
            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION);

            var param = parser.ParseObjectParameter(json);

            param.Type.Should().Be(ContractParameterType.PublicKey);
            param.Value.Should().BeOfType<ECPoint>();
            param.Value.Should().Be(ECPoint.Parse(ON_CURVE_GENERATOR, ECCurve.Secp256r1));
        }

        [Fact]
        public async Task LoadInvocationScriptAsync_reports_an_off_curve_publickey_argument()
        {
            // The ArithmeticException escaped both the ParsePublicKey filter and the
            // invocation-file wrapper, so `neoxp contract invoke --args-file` printed a raw
            // stack trace instead of a clean "invocation file is invalid" error.
            var fileSystem = new MockFileSystem();
            var rootPath = fileSystem.AllDirectories.First();
            var invocationPath = fileSystem.Path.Combine(rootPath, "off-curve.neo-invoke.json");
            var content = "{\"contract\":\"NeoToken\",\"operation\":\"main\","
                + $"\"args\":[{{\"type\":\"PublicKey\",\"value\":\"{OFF_CURVE_PREFIX_02}\"}}]}}";
            fileSystem.AddFile(invocationPath, new MockFileData(content));

            var parser = new ContractParameterParser(DEFAULT_ADDRESS_VERSION, fileSystem: fileSystem);
            var action = () => parser.LoadInvocationScriptAsync(invocationPath);

            var exception = await action.Should().ThrowAsync<Exception>();
            exception.Which.Should().NotBeOfType<ArithmeticException>();
            exception.Which.Message.Should().StartWith($"Invocation file {invocationPath} is invalid:");
            exception.Which.Message.Should().Contain("Invalid PublicKey parameter value");
            exception.Which.InnerException.Should().BeNull();
        }
    }
}
