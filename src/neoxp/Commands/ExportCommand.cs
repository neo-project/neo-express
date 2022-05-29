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
        readonly IExpressChain expressFile;

        public ExportCommand(IExpressChain expressFile)
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
            var folder = fileSystem.Directory.GetCurrentDirectory();

            for (var i = 0; i < expressFile.ConsensusNodes.Count; i++)
            {
                var node = expressFile.ConsensusNodes[i];
                console.Out.WriteLine($"Exporting {node.Wallet.Name} Consensus Node config + wallet");

                var walletPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.wallet.json");
                fileSystem.ExportNEP6(node.Wallet, walletPath, password, expressFile.AddressVersion);

                var nodeConfigPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.config.json");
                using var stream = fileSystem.File.Open(nodeConfigPath, FileMode.Create, FileAccess.Write);
                using var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented };
                using var _ = writer.WriteObject();
                WriteAppConfig(writer, node, password, walletPath);
                WriteProtocolConfig(writer, expressFile);
            }
        }

        internal static void WriteAppConfig(JsonWriter writer, ExpressConsensusNode node, string password, string walletPath)
        {
            // use neo-cli defaults for Logger & Storage

            using var _ = writer.WritePropertyObject("ApplicationConfiguration");

            using (var __ = writer.WritePropertyObject("Storage"))
            {
                writer.WriteProperty("Engine", "MemoryStore");
            }

            using (var __ = writer.WritePropertyObject("P2P"))
            {
                writer.WriteProperty("Port", node.TcpPort);
                writer.WriteProperty("WsPort", node.WebSocketPort);
            }

            using (var __ = writer.WritePropertyObject("UnlockWallet"))
            {
                writer.WriteProperty("Path", walletPath);
                writer.WriteProperty("Password", password);
                writer.WriteProperty("IsActive", true);
            }
        }

        internal static void WriteProtocolConfig(JsonWriter writer, IExpressChain expressFile)
        {
            // use neo defaults for MillisecondsPerBlock

            using var _ = writer.WritePropertyObject("ProtocolConfiguration");

            writer.WriteProperty("Magic", expressFile.Network);
            writer.WriteProperty("AddressVersion", expressFile.AddressVersion);
            writer.WriteProperty("ValidatorsCount", expressFile.ConsensusNodes.Count);

            using (var __ = writer.WritePropertyArray("StandbyCommittee"))
            {
                // TODO expressFile.Chain.GetProtocolSettings
                var settings = expressFile.Chain.GetProtocolSettings();
                for (int i = 0; i < expressFile.ConsensusNodes.Count; i++)
                {
                    var expressAccount = expressFile.ConsensusNodes[i].Wallet.DefaultAccount ?? throw new Exception("Invalid DefaultAccount");
                    var devAccount = DevWalletAccount.FromExpressWalletAccount(settings, expressAccount);
                    var key = devAccount.GetKey();
                    if (key != null)
                    {
                        writer.WriteValue(key.PublicKey.EncodePoint(true).ToHexString());
                    }
                }
            }

            using (var __ = writer.WritePropertyArray("SeedList"))
            {
                foreach (var node in expressFile.ConsensusNodes)
                {
                    writer.WriteValue($"{System.Net.IPAddress.Loopback}:{node.TcpPort}");
                }
            }
        }
    }
}
