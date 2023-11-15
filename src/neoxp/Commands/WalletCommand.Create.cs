// Copyright (C) 2015-2023 The Neo Project.
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
using Neo.BlockchainToolkit.Models;
using NeoExpress.Models;
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
            internal bool Force { get; }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Private key for account (Format: HEX or WIF)\nDefault: Random")]
            internal string PrivateKey { get; set; } = string.Empty;

            internal ExpressWallet Execute()
            {
                var (chainManager, chainPath) = chainManagerFactory.LoadChain(Input);
                var chain = chainManager.Chain;

                if (chain.IsReservedName(Name))
                {
                    throw new Exception($"{Name} is a reserved name. Choose a different wallet name.");
                }

                var existingWallet = chain.GetWallet(Name);
                if (existingWallet is not null)
                {
                    if (!Force)
                    {
                        throw new Exception($"{Name} dev wallet already exists. Use --force to overwrite.");
                    }

                    chain.Wallets.Remove(existingWallet);
                }

                byte[]? priKey = null;
                if (string.IsNullOrEmpty(PrivateKey) == false)
                {
                    try
                    {
                        if (PrivateKey.StartsWith('L'))
                            priKey = Neo.Wallets.Wallet.GetPrivateKeyFromWIF(PrivateKey);
                        else
                            priKey = Convert.FromHexString(PrivateKey);
                    }
                    catch (FormatException)
                    {
                        throw new FormatException("Private key must be in HEX or WIF format.");
                    }
                }

                var wallet = new DevWallet(chainManager.ProtocolSettings, Name);
                var account = priKey == null ? wallet.CreateAccount() : wallet.CreateAccount(priKey!);
                account.IsDefault = true;

                var expressWallet = wallet.ToExpressWallet();
                chain.Wallets ??= new List<ExpressWallet>(1);
                chain.Wallets.Add(expressWallet);
                chainManager.SaveChain(chainPath);
                return expressWallet;
            }

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var wallet = Execute();
                    console.WriteLine(Name);
                    for (int i = 0; i < wallet.Accounts.Count; i++)
                    {
                        console.WriteLine($"    {wallet.Accounts[i].ScriptHash}");
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
