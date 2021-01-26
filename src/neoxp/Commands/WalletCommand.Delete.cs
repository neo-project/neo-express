using System;
using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("delete", Description = "Delete neo-express wallet")]
        class Delete
        {
            readonly IBlockchainOperations chainManager;

            public Delete(IBlockchainOperations chainManager)
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

            internal void Execute()
            {
                var (chain, filename) = chainManager.LoadChain(Input);
                var wallet = chain.GetWallet(Name);

                if (wallet == null)
                {
                    throw new Exception($"{Name} privatenet wallet not found.");
                }
                else
                {
                    if (!Force)
                    {
                        throw new Exception("You must specify force to delete a privatenet wallet.");
                    }

                    chain.Wallets.Remove(wallet);
                    chainManager.SaveChain(chain, filename);
                }
            }

            internal int OnExecute(IConsole console)
            {
                try
                {
                    Execute();
                    console.WriteLine($"{Name} privatenet wallet deleted.");
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
