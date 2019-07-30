using McMaster.Extensions.CommandLineUtils;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace Neo.Express.Commands
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
                    var (devChain, filename) = Express.DevChain.Load(Input);
                    if (devChain.IsReservedName(Name))
                    {
                        throw new Exception($"{Name} is a reserved name. Choose a different wallet name.");
                    }

                    var existingWallet = devChain.GetWallet(Name);
                    if (existingWallet != default)
                    {
                        if (!Force)
                        {
                            throw new Exception($"{Name} dev wallet already exists. Use --force to overwrite.");
                        }

                        devChain.Wallets.Remove(existingWallet);
                    }

                    var wallet = new DevWallet(Name);
                    var account = wallet.CreateAccount();
                    account.IsDefault = true;

                    console.WriteLine($"{Name}\n\t{account.Address}");
                    devChain.Wallets.Add(wallet);

                    devChain.Save(filename);
                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteLine(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }
        }
    }
}
