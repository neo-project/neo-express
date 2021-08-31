using System;
using System.IO;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Models;
using Neo.IO;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("list", Description = "List neo-express wallets")]
        internal class List
        {
            readonly IExpressChainManagerFactory chainManagerFactory;

            public List(IExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal void Execute(TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                var chain = chainManager.Chain;

                for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                {
                    PrintWalletInfo(chain.ConsensusNodes[i].Wallet);
                }

                for (int i = 0; i < chain.Wallets.Count; i++)
                {
                    PrintWalletInfo(chain.Wallets[i]);
                }

                void PrintWalletInfo(ExpressWallet wallet)
                {
                    writer.WriteLine(wallet.Name);

                    foreach (var account in wallet.Accounts)
                    {
                        var devAccount = DevWalletAccount.FromExpressWalletAccount(chainManager.ProtocolSettings, account);
                        var key = devAccount.GetKey() ?? throw new Exception();

                        writer.WriteLine($"  {account.ScriptHash} ({(account.IsDefault ? "Default" : account.Label)})");
                        writer.WriteLine($"    address bytes: {BitConverter.ToString(devAccount.ScriptHash.ToArray())}");
                        writer.WriteLine($"    public key:    {key.PublicKey.EncodePoint(true).ToHexString()}");
                        writer.WriteLine($"    private key:   {key.PrivateKey.ToHexString()}");
                    }
                }
            }

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    Execute(console.Out);
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
