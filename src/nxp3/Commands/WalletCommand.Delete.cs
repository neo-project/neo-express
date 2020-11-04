using System;
using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions;

namespace nxp3.Commands
{
    partial class WalletCommand
    {
        [Command("delete")]
        class Delete
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
                    var wallet = chain.GetWallet(Name);

                    if (wallet == null)
                    {
                        console.WriteLine($"{Name} privatenet wallet not found.");
                    }
                    else
                    {
                        if (!Force)
                        {
                            throw new Exception("You must specify force to delete a privatenet wallet.");
                        }

                        chain.Wallets.Remove(wallet);
                        chain.Save(filename);
                        console.WriteLine($"{Name} privatenet wallet deleted.");
                    }

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
