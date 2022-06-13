using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Models;
using NeoExpress.Models;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("create", Description = "Create neo-express wallet")]
        internal class Create
        {
            readonly IExpressChain chain;

            public Create(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Create(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Wallet name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal int OnExecute(CommandLineApplication app) => app.Execute(this.Execute);

            internal void Execute(IConsole console)
            {
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

                    chain.RemoveWallet(existingWallet);
                }

                var devWallet = new DevWallet(chain.GetProtocolSettings(), Name);
                var devAccount = devWallet.CreateAccount();
                devAccount.IsDefault = true;

                var wallet = devWallet.ToExpressWallet();
                chain.AddWallet(wallet);

                chain.SaveChain();

                if (Json)
                {
                    using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                    using var _ = writer.WriteObject();
                    writer.WriteWallet(wallet);
                }
                else
                {
                    console.Out.WriteWallet(wallet);
                    console.Out.WriteLine("Note: The private keys for the accounts in this wallet are *not* encrypted.");
                    console.Out.WriteLine("      Do not use these accounts on MainNet or in any other system where security is a concern.");
                }
            }
        }
    }
}
