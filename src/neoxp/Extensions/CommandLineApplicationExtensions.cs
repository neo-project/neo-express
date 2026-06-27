// Copyright (C) 2015-2026 The Neo Project.
//
// CommandLineApplicationExtensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using System.Globalization;

namespace NeoExpress
{
    static class CommandLineApplicationExtensions
    {
        // Parse numeric arguments (e.g. decimal GAS amounts and policy values) with the
        // invariant culture so that '.' is always the decimal separator regardless of the
        // host machine's locale. Without this, McMaster parses with the current culture, so
        // on a ',' decimal-separator locale (e.g. de-DE) "1.5" is rejected outright and a
        // value typed as "1,5" is silently read differently than on an en-US machine.
        public static void UseInvariantValueParsing(this CommandLineApplication app)
        {
            app.ValueParsers.ParseCulture = CultureInfo.InvariantCulture;
        }
    }
}
