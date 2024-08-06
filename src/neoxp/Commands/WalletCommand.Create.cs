// Copyright (C) 2015-2024 The Neo Project.
//
// WalletCommand.Create.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Wallets;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("create", Description = "Create neo-express wallet")]
        internal class Create
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Create(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Wallet name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; } = false;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Private key for account (Format: HEX or WIF)\nDefault: Random")]
            internal string PrivateKey { get; set; } = string.Empty;

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManger, chainFilename) = chainManagerFactory.LoadChain(Input);
                    var wallet = chainManger.CreateWallet(Name, PrivateKey, Force);
                    chainManger.SaveChain(chainFilename);

                    console.WriteLine($"Created Wallet {Name}");

                    for (int i = 0; i < wallet.Accounts.Count; i++)
                    {
                        console.WriteLine($"    Address: {wallet.Accounts[i].ScriptHash}");
                    }

                    console.WriteLine("\n\x1b[33mNote: The private keys for the accounts in this wallet are *not* encrypted.\x1b[0m");

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
