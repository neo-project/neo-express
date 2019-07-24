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
                    var input = Program.DefaultPrivatenetFileName(Input);
                    if (!File.Exists(input))
                    {
                        throw new Exception($"{input} doesn't exist");
                    }

                    var devchain = DevChain.Load(input);
                    if (devchain.IsReservedName(Name))
                    {
                        throw new Exception($"{Name} is a reserved name. Choose a different wallet name.");
                    }

                    if (!Force && (devchain.GetWallet(Name) != default))
                    {
                        throw new Exception($"{Name} dev wallet already exists. Use --force to overwrite.");
                    }

                    var wallet = new DevWallet(Name);
                    var account = wallet.CreateAccount();
                    account.IsDefault = true;

                    console.WriteLine($"{Name}\n\t{account.Address}");
                    devchain.Wallets.Add(wallet);

                    devchain.Save(input);
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
