// Copyright (C) 2015-2026 The Neo Project.
//
// ParseQuantityTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using NeoExpress;
using System.Globalization;
using Xunit;

namespace test.workflowvalidation;

public class ParseQuantityTests
{
    [Fact]
    public void ParseQuantity_parses_decimal_with_invariant_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // de-DE uses '.' as a thousands separator, so a culture-sensitive parse
            // would read "1.5" as 15.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var result = TransactionExecutor.ParseQuantity("1.5");

            result.AsT0.Should().Be(1.5m);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
