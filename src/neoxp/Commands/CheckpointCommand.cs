// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("checkpoint", Description = "Manage neo-express checkpoints")]
    [Subcommand(typeof(Create), typeof(Restore), typeof(Run))]
    partial class CheckpointCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }
    }
}
