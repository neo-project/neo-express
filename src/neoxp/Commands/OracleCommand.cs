// Copyright (C) 2023 neo-project
//
// The neo-examples-csharp is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

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
