// Copyright (C) 2015-2026 The Neo Project.
//
// ShowCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("show", Description = "Show information")]
    [Subcommand(typeof(Balance), typeof(Balances), typeof(Block), typeof(Notifications), typeof(Transaction), typeof(NFT), typeof(State))]
    partial class ShowCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }
    }
}
