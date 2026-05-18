// Copyright (C) 2015-2026 The Neo Project.
//
// CandidateListCommandTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress;
using NeoExpress.Commands;
using Newtonsoft.Json.Linq;
using System.Numerics;
using Xunit;

namespace test.workflowvalidation;

public class CandidateListCommandTests
{
    [Fact]
    public async Task WriteCandidatesAsync_writes_json_when_requested()
    {
        var candidates = new[]
        {
            new TransactionExecutor.CandidateInfo(
                "031111111111111111111111111111111111111111111111111111111111111111",
                BigInteger.Parse("12345678901234567890"))
        };
        using var writer = new StringWriter();

        await CandidateCommand.List.WriteCandidatesAsync(writer, candidates, json: true);

        var json = JArray.Parse(writer.ToString());
        json.Should().HaveCount(1);
        json[0]!.Value<string>("public-key").Should().Be(candidates[0].PublicKey);
        json[0]!.Value<string>("votes").Should().Be(candidates[0].Votes.ToString());
    }

    [Fact]
    public async Task WriteCandidatesAsync_preserves_text_output()
    {
        var candidates = new[]
        {
            new TransactionExecutor.CandidateInfo(
                "031111111111111111111111111111111111111111111111111111111111111111",
                new BigInteger(42))
        };
        using var writer = new StringWriter();

        await CandidateCommand.List.WriteCandidatesAsync(writer, candidates, json: false);

        writer.ToString().Should().Be($"{candidates[0].PublicKey,-67}{candidates[0].Votes}{Environment.NewLine}");
    }
}
