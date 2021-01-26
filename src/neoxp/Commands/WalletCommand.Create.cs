using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("create", Description = "Create neo-express wallet")]
        class Create
        {
            readonly IChainManager chainManager;

            public Create(IChainManager chainManager)
            {
                this.chainManager = chainManager;
            }

            [Argument(0, Description = "Wallet name")]
            [Required]
            string Name { get; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            bool Force { get; }

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal ExpressWallet Execute()
            {
                var (chain, filename) = chainManager.Load(Input);
                var existingWallet = chain.GetWallet(Name);
                if (existingWallet != null)
                {
                    if (!Force)
                    {
                        throw new Exception($"{Name} dev wallet already exists. Use --force to overwrite.");
                    }

                    chain.Wallets.Remove(existingWallet);
                }

                var wallet = chainManager.CreateWallet(chain, Name);
                chain.Wallets ??= new List<ExpressWallet>(1);
                chain.Wallets.Add(wallet);
                chainManager.Save(chain, filename);
                return wallet;
            }

            internal int OnExecute(IConsole console)
            {
                try
                {
                    var wallet = Execute();
                    console.WriteLine(Name);
                    foreach (var account in wallet.Accounts)
                    {
                        console.WriteLine($"    {account.ScriptHash}");
                    }
                    console.WriteLine("    Note: The private keys for the accounts in this wallet are *not* encrypted.");
                    console.WriteLine("          Do not use these accounts on MainNet or in any other system where security is a concern.");
                    return 0;
                }
                catch (Exception ex)
                {
                    console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }
        }
    }
}
