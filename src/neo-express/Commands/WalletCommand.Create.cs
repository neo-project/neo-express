using McMaster.Extensions.CommandLineUtils;
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
            private string Name { get; }

            [Option]
            private bool Force { get; }

            [Option]
            private string Input { get; }

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
                    if (existingWallet != default)
                    {
                        if (!Force)
                        {
                            throw new Exception($"{Name} dev wallet already exists. Use --force to overwrite.");
                        }

                        chain.Wallets.Remove(existingWallet);
                    }

                    //var wallet = Program.GetBackend().CreateWallet(Name);
                    //(chain.Wallets ?? (chain.Wallets = new List<ExpressWallet>(1)))
                    //    .Add(wallet);
                    //chain.Save(filename);

                    console.WriteLine(Name);
                    //foreach (var account in wallet.Accounts)
                    //{
                    //    console.WriteLine($"    {account.ScriptHash}");
                    //}
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
