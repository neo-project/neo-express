using System;
using System.IO;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    [Command("export", Description = "Export neo-express protocol, config and wallet files")]
    class ExportCommand
    {
        readonly IExpressChainManagerFactory chainManagerFactory;
        readonly IFileSystem fileSystem;

        public ExportCommand(IExpressChainManagerFactory chainManagerFactory, IFileSystem fileSystem)
        {
            this.chainManagerFactory = chainManagerFactory;
            this.fileSystem = fileSystem;
        }

        [Option(Description = "Path to neo-express data file")]
        string Input { get; } = string.Empty;

        internal void Execute(TextWriter writer)
        {
            var password = Prompt.GetPassword("Input password to use for exported wallets");
            var (chainManager, _) = chainManagerFactory.LoadChain(Input);
            var chain = chainManager.Chain;
            var folder = fileSystem.Directory.GetCurrentDirectory();

            for (var i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var node = chain.ConsensusNodes[i];
                writer.WriteLine($"Exporting {node.Wallet.Name} Conensus Node config + wallet");
                var walletPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.wallet.json");
                ExportNodeWallet(node, walletPath, password);
                var nodeConfigPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.config.json");
                ExportNodeConfig(node, nodeConfigPath, password, walletPath);
            }

            var protocolConfigPath = fileSystem.Path.Combine(folder, "protocol.json");
            ExportProtocolConfig(chain, protocolConfigPath);
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
                console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        void ExportNodeWallet(ExpressConsensusNode node, string path, string password)
        {
            if (fileSystem.File.Exists(path)) fileSystem.File.Delete(path);
            var devWallet = DevWallet.FromExpressWallet(node.Wallet);
            devWallet.Export(path, password);
        }

        void ExportNodeConfig(ExpressConsensusNode node, string path, string password, string walletPath)
        {
            using var stream = fileSystem.File.Open(path, FileMode.Create, FileAccess.Write);
            using var configWriter = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented };

            // use neo-cli defaults for Logger & Storage

            configWriter.WriteStartObject();
            configWriter.WritePropertyName("ApplicationConfiguration");
            configWriter.WriteStartObject();

            configWriter.WritePropertyName("P2P");
            configWriter.WriteStartObject();
            configWriter.WritePropertyName("Port");
            configWriter.WriteValue(node.TcpPort);
            configWriter.WritePropertyName("WsPort");
            configWriter.WriteValue(node.WebSocketPort);
            configWriter.WriteEndObject();

            configWriter.WritePropertyName("UnlockWallet");
            configWriter.WriteStartObject();
            configWriter.WritePropertyName("Path");
            configWriter.WriteValue(walletPath);
            configWriter.WritePropertyName("Password");
            configWriter.WriteValue(password);
            configWriter.WritePropertyName("StartConsensus");
            configWriter.WriteValue(true);
            configWriter.WritePropertyName("IsActive");
            configWriter.WriteValue(true);
            configWriter.WriteEndObject();

            configWriter.WriteEndObject();
            configWriter.WriteEndObject();
        }

        void ExportProtocolConfig(ExpressChain chain, string path)
        {
            using var stream = fileSystem.File.Open(path, FileMode.Create, FileAccess.Write);
            using var protocolWriter = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented };

            // use neo defaults for MillisecondsPerBlock & AddressVersion

            protocolWriter.WriteStartObject();
            protocolWriter.WritePropertyName("ProtocolConfiguration");
            protocolWriter.WriteStartObject();

            protocolWriter.WritePropertyName("Magic");
            protocolWriter.WriteValue(chain.Magic);
            protocolWriter.WritePropertyName("ValidatorsCount");
            protocolWriter.WriteValue(chain.ConsensusNodes.Count);

            protocolWriter.WritePropertyName("StandbyCommittee");
            protocolWriter.WriteStartArray();
            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var expressAccount = chain.ConsensusNodes[i].Wallet.DefaultAccount ?? throw new Exception("Invalid DefaultAccount");
                var devAccount = DevWalletAccount.FromExpressWalletAccount(expressAccount);
                var key = devAccount.GetKey();
                if (key != null)
                {
                    protocolWriter.WriteValue(key.PublicKey.EncodePoint(true).ToHexString());
                }
            }
            protocolWriter.WriteEndArray();

            protocolWriter.WritePropertyName("SeedList");
            protocolWriter.WriteStartArray();
            foreach (var node in chain.ConsensusNodes)
            {
                protocolWriter.WriteValue($"{System.Net.IPAddress.Loopback}:{node.TcpPort}");
            }
            protocolWriter.WriteEndArray();

            protocolWriter.WriteEndObject();
            protocolWriter.WriteEndObject();
        }
    }
}
