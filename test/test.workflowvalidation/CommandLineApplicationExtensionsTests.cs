// Copyright (C) 2015-2026 The Neo Project.
//
// CommandLineApplicationExtensionsTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress;
using System;
using System.Globalization;
using Xunit;

namespace test.workflowvalidation;

public class CommandLineApplicationExtensionsTests
{
    class DecimalModel
    {
        [Option("--value")]
        public decimal Value { get; init; }
    }

    [Fact]
    public void UseInvariantValueParsing_parses_a_period_decimal_under_a_comma_locale()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // de-DE uses ',' as the decimal separator. Without invariant parsing,
            // McMaster would reject "1.5" or read it as a different value.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var app = new CommandLineApplication<DecimalModel>();
            app.UseInvariantValueParsing();
            app.Conventions.UseDefaultConventions();

            app.Parse("--value", "1.5");

            app.Model.Value.Should().Be(1.5m);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
