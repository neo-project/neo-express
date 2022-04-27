using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("delete", Description = "Delete neo-express wallet")]
        internal class Delete
        {
            readonly IFileSystem fileSystem;

            public Delete(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            [Argument(0, Description = "Wallet name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal void Execute()
            {
                var (chainManager, chainPath) = fileSystem.LoadChainManager(Input);
                var chain = chainManager;
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
                    fileSystem.SaveChain(chain, chainPath);
                }
            }

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    Execute();
                    console.WriteLine($"{Name} privatenet wallet deleted.");
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
