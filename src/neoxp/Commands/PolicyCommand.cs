// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("policy", Description = "Manage blockchain policy")]
    [Subcommand(typeof(Block), typeof(Get), typeof(IsBlocked), typeof(Set), typeof(Sync), typeof(Unblock))]
    partial class PolicyCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }
    }
}
