using System;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using NeoExpress.Models;
using Newtonsoft.Json;
using FileMode = System.IO.FileMode;
using FileAccess = System.IO.FileAccess;
using StreamWriter = System.IO.StreamWriter;

namespace NeoExpress.Commands
{
    [Command("export", Description = "Export neo-express protocol, config and wallet files")]
    class ExportCommand
    {
        readonly IExpressFile expressFile;

        public ExportCommand(IExpressFile expressFile)
        {
            this.expressFile = expressFile;
        }

        public ExportCommand(CommandLineApplication app) : this(app.GetExpressFile())
        {
        }

        internal int OnExecute(CommandLineApplication app) => app.Execute(this.Execute);

        internal void Execute(IFileSystem fileSystem, IConsole console)
        {
            var password = Prompt.GetPassword("Input password to use for exported wallets");
            var chain = expressFile.Chain;
            var folder = fileSystem.Directory.GetCurrentDirectory();

            for (var i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var node = chain.ConsensusNodes[i];
                console.Out.WriteLine($"Exporting {node.Wallet.Name} Consensus Node config + wallet");

                var walletPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.wallet.json");
                fileSystem.ExportNEP6(node.Wallet, walletPath, password, chain.AddressVersion);

                var nodeConfigPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.config.json");
                using var stream = fileSystem.File.Open(nodeConfigPath, FileMode.Create, FileAccess.Write);
                using var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented };
                ExportNodeConfig(writer, chain.GetProtocolSettings(), chain, node, password, walletPath);
            }
        }

        internal static void ExportNodeConfig(JsonWriter writer, ProtocolSettings settings, ExpressChain chain, ExpressConsensusNode node, string password, string walletPath)
        {
            // use neo-cli defaults for Logger & Storage

            using var _ = writer.WriteStartObjectAuto();

            writer.WritePropertyName("ApplicationConfiguration");
            {
                using var __ = writer.WriteStartObjectAuto();

                writer.WritePropertyName("Storage");
                {
                    using var ___ = writer.WriteStartObjectAuto();
                    writer.WriteProperty("Engine", "MemoryStore");
                }

                writer.WritePropertyName("P2P");
                {
                    using var ___ = writer.WriteStartObjectAuto();
                    writer.WriteProperty("Port", node.TcpPort);
                    writer.WriteProperty("WsPort", node.WebSocketPort);
                }

                writer.WritePropertyName("UnlockWallet");
                {
                    using var ___ = writer.WriteStartObjectAuto();
                    writer.WriteProperty("Path", walletPath);
                    writer.WriteProperty("Password", password);
                    writer.WriteProperty("IsActive", true);
                }
            }

            WriteProtocolConfiguration(writer, settings, chain);
        }

        internal static void WriteProtocolConfiguration(JsonWriter writer, ProtocolSettings settings, ExpressChain chain)
        {
            // use neo defaults for MillisecondsPerBlock

            writer.WritePropertyName("ProtocolConfiguration");
            using var _ = writer.WriteStartObjectAuto();

            writer.WriteProperty("Magic", chain.Network);
            writer.WriteProperty("AddressVersion", settings.AddressVersion);
            writer.WriteProperty("ValidatorsCount", chain.ConsensusNodes.Count);

            writer.WritePropertyName("StandbyCommittee");
            {
                using var __ = writer.WriteStartArrayAuto();
                for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                {
                    var expressAccount = chain.ConsensusNodes[i].Wallet.DefaultAccount ?? throw new Exception("Invalid DefaultAccount");
                    var devAccount = DevWalletAccount.FromExpressWalletAccount(settings, expressAccount);
                    var key = devAccount.GetKey();
                    if (key != null)
                    {
                        writer.WriteValue(key.PublicKey.EncodePoint(true).ToHexString());
                    }
                }
            }

            writer.WritePropertyName("SeedList");
            {
                using var __ = writer.WriteStartArrayAuto();
                foreach (var node in chain.ConsensusNodes)
                {
                    writer.WriteValue($"{System.Net.IPAddress.Loopback}:{node.TcpPort}");
                }
            }
        }
    }
}
