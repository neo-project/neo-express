// Copyright (C) 2015-2024 The Neo Project.
//
// ExportCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Extensions;
using NeoExpress.Models;
using Newtonsoft.Json;
using System.IO.Abstractions;

namespace NeoExpress.Commands
{
    [Command("export", Description = "Export neo-express protocol, config and wallet files")]
    class ExportCommand
    {
        readonly ExpressChainManagerFactory chainManagerFactory;
        readonly IFileSystem fileSystem;

        public ExportCommand(ExpressChainManagerFactory chainManagerFactory, IFileSystem fileSystem)
        {
            this.chainManagerFactory = chainManagerFactory;
            this.fileSystem = fileSystem;
        }

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        internal void Execute(System.IO.TextWriter writer)
        {
            var password = Prompt.GetPassword("Input password to use for exported wallets");
            var (chainManager, _) = chainManagerFactory.LoadChain(Input);
            var chain = chainManager.Chain;
            var folder = fileSystem.Directory.GetCurrentDirectory();

            for (var i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var node = chain.ConsensusNodes[i];
                writer.WriteLine($"Exporting {node.Wallet.Name} Consensus Node config + wallet");
                var walletPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.wallet.json");
                ExportNodeWallet(chainManager.ProtocolSettings, node, walletPath, password);
                var nodeConfigPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.config.json");
                ExportNodeConfig(chainManager.ProtocolSettings, chain, node, nodeConfigPath, password, walletPath);
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

        void ExportNodeWallet(ProtocolSettings settings, ExpressConsensusNode node, string path, string password)
        {
            if (fileSystem.File.Exists(path))
                fileSystem.File.Delete(path);
            var devWallet = DevWallet.FromExpressWallet(settings, node.Wallet);
            devWallet.Export(path, password);
        }

        void ExportNodeConfig(ProtocolSettings settings, ExpressChain chain, ExpressConsensusNode node, string path, string password, string walletPath)
        {
            using var stream = fileSystem.File.Open(path, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            using var writer = new JsonTextWriter(new System.IO.StreamWriter(stream)) { Formatting = Formatting.Indented };

            // use neo-cli defaults for Logger & Storage

            writer.WriteStartObject();
            writer.WritePropertyName("ApplicationConfiguration");
            writer.WriteStartObject();

            writer.WritePropertyName("Storage");
            writer.WriteStartObject();
            writer.WritePropertyName("Engine");
            writer.WriteValue("MemoryStore");
            writer.WriteEndObject();

            writer.WritePropertyName("P2P");
            writer.WriteStartObject();
            writer.WritePropertyName("Port");
            writer.WriteValue(node.TcpPort);
            writer.WriteEndObject();

            writer.WritePropertyName("UnlockWallet");
            writer.WriteStartObject();
            writer.WritePropertyName("Path");
            writer.WriteValue(walletPath);
            writer.WritePropertyName("Password");
            writer.WriteValue(password);
            writer.WritePropertyName("IsActive");
            writer.WriteValue(true);
            writer.WriteEndObject();

            writer.WriteEndObject();

            WriteProtocolConfiguration(writer, settings, chain);

            writer.WriteEndObject();
        }

        void WriteProtocolConfiguration(JsonTextWriter writer, ProtocolSettings settings, ExpressChain chain)
        {
            // use neo defaults for MillisecondsPerBlock

            writer.WritePropertyName("ProtocolConfiguration");
            writer.WriteStartObject();

            writer.WritePropertyName("Magic");
            writer.WriteValue(chain.Network);
            writer.WritePropertyName("AddressVersion");
            writer.WriteValue(settings.AddressVersion);
            writer.WritePropertyName("ValidatorsCount");
            writer.WriteValue(chain.ConsensusNodes.Count);

            writer.WritePropertyName("StandbyCommittee");
            writer.WriteStartArray();
            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var expressAccount = chain.ConsensusNodes[i].Wallet.DefaultAccount ?? throw new Exception("Invalid DefaultAccount");
                var devAccount = DevWalletAccount.FromExpressWalletAccount(settings, expressAccount);
                var key = devAccount.GetKey();
                if (key is not null)
                {
                    writer.WriteValue(key.PublicKey.EncodePoint(true).ToHexString());
                }
            }
            writer.WriteEndArray();

            writer.WritePropertyName("SeedList");
            writer.WriteStartArray();
            foreach (var node in chain.ConsensusNodes)
            {
                writer.WriteValue($"{System.Net.IPAddress.Loopback}:{node.TcpPort}");
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}
