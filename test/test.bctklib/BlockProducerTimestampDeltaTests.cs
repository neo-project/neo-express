// Copyright (C) 2015-2026 The Neo Project.
//
// BlockProducerTimestampDeltaTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo.BlockchainToolkit;
using System;
using Xunit;

namespace test.bctklib
{
    public class BlockProducerTimestampDeltaTests
    {
        // regression test: ulong seconds values beyond TimeSpan's representable
        // range used to escape ParseTimestampDelta as a raw OverflowException
        // instead of the method's friendly validation error (round-7 fuzz finding)
        [Fact]
        public void parse_timestamp_delta_rejects_huge_seconds_with_clean_validation_error()
        {
            const string hugeSeconds = "7962345847088288023"; // < ulong.MaxValue, > TimeSpan range

            var act = () => BlockProducer.ParseTimestampDelta(hugeSeconds);

            var exception = act.Should().Throw<Exception>().Which;
            exception.Should().NotBeOfType<OverflowException>();
            exception.Message.Should().Be($"Timestamp delta {hugeSeconds} is too large");
        }

        [Fact]
        public void parse_timestamp_delta_accepts_seconds_up_to_the_timespan_max()
        {
            // (ulong)TimeSpan.MaxValue.TotalSeconds truncates to 922337203685 — the
            // largest whole-second value the guard still lets through. FromSeconds
            // converts through a double, so compare TotalSeconds approximately
            // instead of asserting exact TimeSpan equality.
            var delta = BlockProducer.ParseTimestampDelta("922337203685");

            delta.TotalSeconds.Should().BeApproximately(922337203685, 1);
        }

        [Fact]
        public void parse_timestamp_delta_rejects_seconds_one_past_the_timespan_max()
        {
            const string tooLarge = "922337203686";

            var act = () => BlockProducer.ParseTimestampDelta(tooLarge);

            var exception = act.Should().Throw<Exception>().Which;
            exception.Should().NotBeOfType<OverflowException>();
            exception.Message.Should().Be($"Timestamp delta {tooLarge} is too large");
        }

        [Theory]
        [InlineData("5", 5ul)]
        [InlineData("0", 0ul)]
        public void parse_timestamp_delta_accepts_second_counts(string input, ulong seconds)
        {
            var delta = BlockProducer.ParseTimestampDelta(input);

            delta.Should().Be(TimeSpan.FromSeconds(seconds));
        }

        [Fact]
        public void parse_timestamp_delta_accepts_a_timespan_formatted_input()
        {
            var delta = BlockProducer.ParseTimestampDelta("1.00:00:00");

            delta.Should().Be(TimeSpan.FromDays(1));
        }

        [Fact]
        public void parse_timestamp_delta_returns_zero_for_null_or_empty_input()
        {
            string? nullInput = null;

            BlockProducer.ParseTimestampDelta(nullInput!).Should().Be(TimeSpan.Zero);
            BlockProducer.ParseTimestampDelta("").Should().Be(TimeSpan.Zero);
        }

        [Fact]
        public void parse_timestamp_delta_rejects_unparseable_input()
        {
            const string unparseable = "not-a-timestamp-delta";

            var act = () => BlockProducer.ParseTimestampDelta(unparseable);

            act.Should().Throw<Exception>()
                .WithMessage($"Could not parse timestamp delta {unparseable}");
        }

        [Theory]
        [InlineData("-10")]
        [InlineData("-1.00:00:00")]
        public void parse_timestamp_delta_rejects_negative_input(string input)
        {
            var act = () => BlockProducer.ParseTimestampDelta(input);

            act.Should().Throw<Exception>()
                .WithMessage($"*{input}*");
        }
    }
}
