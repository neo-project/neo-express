// Copyright (C) 2015-2024 The Neo Project.
//
// WalletCommand.Delete.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("delete", Description = "Delete neo-express wallet")]
        internal class Delete
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Delete(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Wallet name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal void Execute()
            {
                var (chainManager, chainPath) = chainManagerFactory.LoadChain(Input);
                var chain = chainManager.Chain;
                var wallet = chain.GetWallet(Name);

                if (wallet is null)
                {
                    throw new Exception($"{Name} privatenet wallet not found.");
                }
                else
                {
                    if (!Force)
                    {
                        throw new Exception("You must specify force to delete a privatenet wallet.");
                    }

                    chain.Wallets.Remove(wallet);
                    chainManager.SaveChain(chainPath);
                }
            }

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    Execute();
                    console.WriteLine($"{Name} privatenet wallet deleted.");
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }
        }
    }
}
