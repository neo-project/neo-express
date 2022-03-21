using System;
using System.IO;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Models;
using Neo.IO;
using NeoExpress.Models;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("list", Description = "List neo-express wallets")]
        internal class List
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public List(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    var chain = chainManager.Chain;

                    if (Json)
                    {
                        using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                        writer.WriteStartObject();

                        foreach (var node in chain.ConsensusNodes)
                        {
                            PrintWalletInfo(node.Wallet);
                        }

                        foreach (var wallet in chain.Wallets)
                        {
                            PrintWalletInfo(wallet);
                        }

                        writer.WriteEndObject();

                        void PrintWalletInfo(ExpressWallet wallet)
                        {
                            writer.WritePropertyName(wallet.Name);

                            writer.WriteStartArray();
                            foreach (var account in wallet.Accounts)
                            {
                                var devAccount = DevWalletAccount.FromExpressWalletAccount(chainManager.ProtocolSettings, account);
                                var keyPair = devAccount.GetKey() ?? throw new Exception();

                                writer.WriteStartObject();
                                writer.WritePropertyName("name");
                                writer.WriteValue(account.IsDefault ? "Default" : account.Label);
                                writer.WritePropertyName("address");
                                writer.WriteValue(devAccount.Address);
                                writer.WritePropertyName("script-hash");
                                writer.WriteValue(devAccount.ScriptHash.ToString());
                                writer.WritePropertyName("private-key");
                                writer.WriteValue(keyPair.PrivateKey.ToHexString());
                                writer.WritePropertyName("public-key");
                                writer.WriteValue(keyPair.PublicKey.EncodePoint(true).ToHexString());
                                writer.WriteEndObject();
                            }
                            writer.WriteEndArray();
                        }
                    }
                    else
                    {
                        var writer = console.Out;
                        foreach (var node in chain.ConsensusNodes)
                        {
                            PrintWalletInfo(node.Wallet);
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
                                var devAccount = DevWalletAccount.FromExpressWalletAccount(chainManager.ProtocolSettings, account);
                                var keyPair = devAccount.GetKey() ?? throw new Exception();

                                writer.WriteLine($"  {devAccount.Address} ({(account.IsDefault ? "Default" : account.Label)})");
                                writer.WriteLine($"    script hash: {BitConverter.ToString(devAccount.ScriptHash.ToArray())}");
                                writer.WriteLine($"    public key:    {keyPair.PublicKey.EncodePoint(true).ToHexString()}");
                                writer.WriteLine($"    private key:   {keyPair.PrivateKey.ToHexString()}");
                            }
                        }
                    }

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
