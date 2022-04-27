using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit;
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
            readonly IFileSystem fileSystem;

            public List(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = fileSystem.LoadChainManager(Input);
                    var settings = chain.GetProtocolSettings();

                    var genesis = chain.GetGenesisAccount(settings);
                    var genesisAccount = genesis.wallet.GetAccount(genesis.accountHash)
                        ?? throw new Exception("Failed to retrieve genesis account");

                    if (Json)
                    {
                        using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                        writer.WriteStartObject();

                        writer.WritePropertyName(ExpressChainExtensions.GENESIS);
                        PrintAccountInfo(writer, ExpressChainExtensions.GENESIS, genesisAccount);

                        foreach (var node in chain.ConsensusNodes)
                        {
                            PrintWalletInfo(writer, node.Wallet, settings);
                        }

                        foreach (var wallet in chain.Wallets)
                        {
                            PrintWalletInfo(writer, wallet, settings);
                        }

                        writer.WriteEndObject();

                        static void PrintWalletInfo(JsonTextWriter writer, ExpressWallet wallet, Neo.ProtocolSettings protocolSettings)
                        {
                            writer.WritePropertyName(wallet.Name);

                            writer.WriteStartArray();
                            foreach (var account in wallet.Accounts)
                            {
                                var devAccount = DevWalletAccount.FromExpressWalletAccount(protocolSettings, account);
                                PrintAccountInfo(writer, wallet.Name, devAccount);
                            }
                            writer.WriteEndArray();
                        }

                        static void PrintAccountInfo(JsonTextWriter writer, string walletName, Neo.Wallets.WalletAccount account)
                        {
                            var keyPair = account.GetKey() ?? throw new Exception();

                            writer.WriteStartObject();
                            writer.WritePropertyName("account-name");
                            writer.WriteValue(walletName);
                            writer.WritePropertyName("account-label");
                            writer.WriteValue(account.IsDefault ? "Default" : account.Label);
                            writer.WritePropertyName("address");
                            writer.WriteValue(account.Address);
                            writer.WritePropertyName("script-hash");
                            writer.WriteValue(account.ScriptHash.ToString());
                            writer.WritePropertyName("private-key");
                            writer.WriteValue(keyPair.PrivateKey.ToHexString());
                            writer.WritePropertyName("public-key");
                            writer.WriteValue(keyPair.PublicKey.EncodePoint(true).ToHexString());
                            writer.WriteEndObject();
                        }
                    }
                    else
                    {
                        var writer = console.Out;

                        writer.WriteLine(ExpressChainExtensions.GENESIS);
                        PrintAccountInfo(writer, genesisAccount);

                        foreach (var node in chain.ConsensusNodes)
                        {
                            PrintWalletInfo(writer, node.Wallet, settings);
                        }

                        foreach (var wallet in chain.Wallets)
                        {
                            PrintWalletInfo(writer, wallet, settings);
                        }

                        static void PrintWalletInfo(TextWriter writer, ExpressWallet wallet, Neo.ProtocolSettings protocolSettings)
                        {
                            writer.WriteLine(wallet.Name);

                            foreach (var account in wallet.Accounts)
                            {
                                var devAccount = DevWalletAccount.FromExpressWalletAccount(protocolSettings, account);
                                PrintAccountInfo(writer, devAccount);
                            }
                        }

                        static void PrintAccountInfo(TextWriter writer, Neo.Wallets.WalletAccount account)
                        {
                            var keyPair = account.GetKey() ?? throw new Exception();

                            writer.WriteLine($"  {account.Address} ({(account.IsDefault ? "Default" : account.Label)})");
                            writer.WriteLine($"    script hash: {BitConverter.ToString(account.ScriptHash.ToArray())}");
                            writer.WriteLine($"    public key:    {keyPair.PublicKey.EncodePoint(true).ToHexString()}");
                            writer.WriteLine($"    private key:   {keyPair.PrivateKey.ToHexString()}");
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
