// Copyright (C) 2015-2024 The Neo Project.
//
// ContractCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

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
        typeof(Update),
        typeof(Validate))]
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
