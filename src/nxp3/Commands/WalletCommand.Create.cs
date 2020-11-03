using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions;
using NeoExpress.Abstractions.Models;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    partial class WalletCommand
    {
        [Command("create")]
        class Create
        {
            [Argument(0)]
            [Required]
            string Name { get; } = string.Empty;

            [Option]
            bool Force { get; }

            [Option]
            string Input { get; } = string.Empty;

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, filename) = Program.LoadExpressChain(Input);
                    var existingWallet = chain.GetWallet(Name);
                    if (existingWallet != null)
                    {
                        if (!Force)
                        {
                            throw new Exception($"{Name} dev wallet already exists. Use --force to overwrite.");
                        }

                        chain.Wallets.Remove(existingWallet);
                    }

                    var blockchainOperations = new BlockchainOperations();
                    var wallet = blockchainOperations.CreateWallet(chain, Name);
                    chain.Wallets ??= new List<ExpressWallet>(1);
                    chain.Wallets.Add(wallet);
                    chain.Save(filename);

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
                    console.WriteLine(ex.Message);
                    return 1;
                }
            }
        }
    }
}
