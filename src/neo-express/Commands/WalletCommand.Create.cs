using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions;
using NeoExpress.Abstractions.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands
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

                    var blockchainOperations = new NeoExpress.Neo2.BlockchainOperations();
                    var wallet = blockchainOperations.CreateWallet(chain, Name, Force);
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
