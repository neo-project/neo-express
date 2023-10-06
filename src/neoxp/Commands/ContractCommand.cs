// Copyright (C) 2023 neo-project
//
// The neo-examples-csharp is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("contract", Description = "Manage smart contracts")]
    [Subcommand(
        typeof(Deploy),
        typeof(Download),
        typeof(Get),
        typeof(Hash),
        typeof(Invoke),
        typeof(List),
        typeof(Run),
        typeof(Storage),
        typeof(Update))]
    partial class ContractCommand
    {
        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp(false);
            return 1;
        }
    }
}
