// Copyright (C) 2015-2024 The Neo Project.
//
// WalletCommand.List.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Models;
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

                    var genesis = chain.GetGenesisAccount(chainManager.ProtocolSettings);
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
                            PrintWalletInfo(writer, node.Wallet, chainManager.ProtocolSettings);
                        }

                        foreach (var wallet in chain.Wallets)
                        {
                            PrintWalletInfo(writer, wallet, chainManager.ProtocolSettings);
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
                            writer.WriteValue(Convert.ToHexString(keyPair.PrivateKey));
                            writer.WritePropertyName("private-key-wif");
                            writer.WriteValue(keyPair.Export());
                            writer.WritePropertyName("public-key");
                            writer.WriteValue(Convert.ToHexString(keyPair.PublicKey.EncodePoint(true)));
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
                            PrintWalletInfo(writer, node.Wallet, chainManager.ProtocolSettings);
                        }

                        foreach (var wallet in chain.Wallets)
                        {
                            PrintWalletInfo(writer, wallet, chainManager.ProtocolSettings);
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
                            writer.WriteLine($"    script hash:       {account.ScriptHash.ToString()}");
                            writer.WriteLine($"    public key:        {Convert.ToHexString(keyPair.PublicKey.EncodePoint(true))}");
                            writer.WriteLine($"    private key:       {Convert.ToHexString(keyPair.PrivateKey)}");
                            writer.WriteLine($"    private key (WIF): {keyPair.Export()}");
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
