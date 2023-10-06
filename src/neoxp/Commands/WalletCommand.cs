// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("wallet", Description = "Manage neo-express wallets")]
    [Subcommand(typeof(Create), typeof(Delete), typeof(Export), typeof(List))]
    partial class WalletCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }
    }
}
