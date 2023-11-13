// Copyright (C) 2015-2023 The Neo Project.
//
// OracleCommand.cs is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("oracle", Description = "Manage oracle nodes and requests")]
    [Subcommand(typeof(Enable), typeof(List), typeof(Requests), typeof(Response))]
    partial class OracleCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }
    }
}
