using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("create", Description = "Create neo-express wallet")]
        class Create
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public Create(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Wallet name")]
            [Required]
            string Name { get; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            bool Force { get; }

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal ExpressWallet Execute()
            {
                var (chainManager, chainPath) = chainManagerFactory.LoadChain(Input);
                var chain = chainManager.Chain;
                
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

                var wallet = new DevWallet(Name);
                var account = wallet.CreateAccount();
                account.IsDefault = true;
                
                var expressWallet = wallet.ToExpressWallet();
                chain.Wallets ??= new List<ExpressWallet>(1);
                chain.Wallets.Add(expressWallet);
                chainManager.SaveChain(chainPath);
                return expressWallet;
            }

            internal int OnExecute(IConsole console)
            {
                try
                {
                    var wallet = Execute();
                    console.WriteLine(Name);
                    for (int i = 0; i < wallet.Accounts.Count; i++)
                    {
                        console.WriteLine($"    {wallet.Accounts[i].ScriptHash}");
                    }
                    console.WriteLine("    Note: The private keys for the accounts in this wallet are *not* encrypted.");
                    console.WriteLine("          Do not use these accounts on MainNet or in any other system where security is a concern.");
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
