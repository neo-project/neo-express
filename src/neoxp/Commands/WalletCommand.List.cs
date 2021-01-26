using System;
using System.IO;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using Neo.IO;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("list", Description = "List neo-express wallets")]
        class List
        {
            readonly IBlockchainOperations chainManager;

            public List(IBlockchainOperations chainManager)
            {
                this.chainManager = chainManager;
            }

            [Option(Description = "Path to neo-express data file")]
            string Input { get; } = string.Empty;

            internal void Execute(TextWriter writer)
            {
                var (chain, _) = chainManager.LoadChain(Input);

                foreach (var wallet in chain.ConsensusNodes.Select(n => n.Wallet))
                {
                    PrintWalletInfo(wallet);
                }

                foreach (var wallet in chain.Wallets)
                {
                    PrintWalletInfo(wallet);
                }

                void PrintWalletInfo(ExpressWallet wallet)
                {
                    writer.WriteLine(wallet.Name);

                    foreach (var account in wallet.Accounts)
                    {
                        var devAccount = DevWalletAccount.FromExpressWalletAccount(account);
                        var key = devAccount.GetKey() ?? throw new Exception();

                        writer.WriteLine($"  {account.ScriptHash} ({(account.IsDefault ? "Default" : account.Label)})");
                        writer.WriteLine($"    address bytes: {BitConverter.ToString(devAccount.ScriptHash.ToArray())}");
                        writer.WriteLine($"    public key:    {key.PublicKey.EncodePoint(true).ToHexString()}");
                        writer.WriteLine($"    private key:   {key.PrivateKey.ToHexString()}");
                    }
                }
            }

            internal int OnExecute(IConsole console)
            {
                try
                {
                    Execute(console.Out);
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
