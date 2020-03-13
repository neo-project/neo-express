using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo2.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Neo2.Commands
{
    internal partial class WalletCommand
    {
        [Command("create")]
        private class Create
        {
            [Argument(0)]
            [Required]
            private string Name { get; } = string.Empty;

            [Option]
            private bool Force { get; }

            [Option]
            private string Input { get; } = string.Empty;

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, filename) = Program.LoadExpressChain(Input);
                    if (chain.IsReservedName(Name))
                    {
                        throw new Exception($"{Name} is a reserved name. Choose a different wallet name.");
                    }

                    var existingWallet = chain.GetWallet(Name);
                    if (existingWallet != null)
                    {
                        if (!Force)
                        {
                            throw new Exception($"{Name} dev wallet already exists. Use --force to overwrite.");
                        }

                        chain.Wallets.Remove(existingWallet);
                    }

                    var wallet = BlockchainOperations.CreateWallet(Name);
                    (chain.Wallets ?? (chain.Wallets = new List<ExpressWallet>(1)))
                        .Add(wallet);
                    chain.Save(filename);

                    console.WriteLine(Name);
                    foreach (var account in wallet.Accounts)
                    {
                        console.WriteLine($"    {account.ScriptHash}");
                    }
                    console.WriteWarning("    Note: The private keys for the accounts in this wallet are *not* encrypted.");
                    console.WriteWarning("          Do not use these accounts on MainNet or in any other system where security is a concern.");

                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteError(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }
        }
    }
}
