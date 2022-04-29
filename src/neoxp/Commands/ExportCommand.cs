using System;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using NeoExpress.Models;
using Newtonsoft.Json;
using Nito.Disposables;

namespace NeoExpress.Commands
{
    [Command("export", Description = "Export neo-express protocol, config and wallet files")]
    class ExportCommand
    {
        readonly IFileSystem fileSystem;

        public ExportCommand(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        
        internal string Input { get; init; } = string.Empty;

        internal void Execute(System.IO.TextWriter writer)
        {
            var password = Prompt.GetPassword("Input password to use for exported wallets");
            var (chain, _) = fileSystem.LoadExpressChain(Input);
            var folder = fileSystem.Directory.GetCurrentDirectory();

            var settings = chain.GetProtocolSettings();
            for (var i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var node = chain.ConsensusNodes[i];
                writer.WriteLine($"Exporting {node.Wallet.Name} Consensus Node config + wallet");
                var walletPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.wallet.json");
                ExportNodeWallet(settings, node, walletPath, password);
                var nodeConfigPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.config.json");
                ExportNodeConfig(settings, chain, node, nodeConfigPath, password, walletPath);
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
            if (fileSystem.File.Exists(path)) fileSystem.File.Delete(path);
            fileSystem.ExportNEP6(node.Wallet, path, password, settings.AddressVersion);
        }

        void ExportNodeConfig(ProtocolSettings settings, ExpressChain chain, ExpressConsensusNode node, string path, string password, string walletPath)
        {
            using var stream = fileSystem.File.Open(path, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            using var writer = new JsonTextWriter(new System.IO.StreamWriter(stream)) { Formatting = Formatting.Indented };

            // use neo-cli defaults for Logger & Storage

            using var _ = writer.WriteStartObjectAuto();

            writer.WritePropertyName("ApplicationConfiguration");
            {
                using var __ = writer.WriteStartObjectAuto();

                writer.WritePropertyName("Storage");
                {
                    using var ___ = writer.WriteStartObjectAuto();
                    writer.WritePropertyName("Engine");
                    writer.WriteValue("MemoryStore");
                }

                writer.WritePropertyName("P2P");
                {
                    using var ___ = writer.WriteStartObjectAuto();
                    writer.WritePropertyName("Port");
                    writer.WriteValue(node.TcpPort);
                    writer.WritePropertyName("WsPort");
                    writer.WriteValue(node.WebSocketPort);
                }

                writer.WritePropertyName("UnlockWallet");
                {
                    using var ___ = writer.WriteStartObjectAuto();
                    writer.WritePropertyName("Path");
                    writer.WriteValue(walletPath);
                    writer.WritePropertyName("Password");
                    writer.WriteValue(password);
                    writer.WritePropertyName("IsActive");
                    writer.WriteValue(true);
                }
            }

            WriteProtocolConfiguration(writer, settings, chain);
        }

        void WriteProtocolConfiguration(JsonTextWriter writer, ProtocolSettings settings, ExpressChain chain)
        {
            // use neo defaults for MillisecondsPerBlock

            writer.WritePropertyName("ProtocolConfiguration");
            using var _ = writer.WriteStartObjectAuto();

            writer.WritePropertyName("Magic");
            writer.WriteValue(chain.Network);
            writer.WritePropertyName("AddressVersion");
            writer.WriteValue(settings.AddressVersion);
            writer.WritePropertyName("ValidatorsCount");
            writer.WriteValue(chain.ConsensusNodes.Count);

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
