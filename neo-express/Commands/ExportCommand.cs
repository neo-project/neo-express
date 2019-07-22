using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

namespace Neo.Express.Commands
{
    [Command("export")]
    internal class ExportCommand
    {
        [Option]
        private string Input { get; }

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            var input = string.IsNullOrEmpty(Input)
                ? Path.Combine(Directory.GetCurrentDirectory(), "express.privatenet.json")
                : Input;

            if (!File.Exists(input))
            {
                console.WriteLine($"{input} doesn't exist");
                app.ShowHelp();
                return 1;
            }

            var chain = DevChain.Load(input);

            var password = Prompt.GetPassword("Input password to use for exported wallets");

            for (var i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var consensusNode = chain.ConsensusNodes[i];
                console.WriteLine($"Exporting {consensusNode.Wallet.Name} Conensus Node wallet");

                var walletPath = Path.Combine(Directory.GetCurrentDirectory(), $"{consensusNode.Wallet.Name}.wallet.json");
                if (File.Exists(walletPath))
                {
                    File.Delete(walletPath);
                }

                consensusNode.Wallet.Export(walletPath, password);

                using (var stream = File.Open(Path.Combine(Directory.GetCurrentDirectory(), $"{consensusNode.Wallet.Name}.config.json"), FileMode.Create, FileAccess.Write))
                using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("ApplicationConfiguration");
                    writer.WriteStartObject();

                    writer.WritePropertyName("Paths");
                    writer.WriteStartObject();
                    writer.WritePropertyName("Chain");
                    writer.WriteValue("Chain_{0}");
                    writer.WriteEndObject();

                    writer.WritePropertyName("P2P");
                    writer.WriteStartObject();
                    writer.WritePropertyName("Port");
                    writer.WriteValue(consensusNode.TcpPort);
                    writer.WritePropertyName("WsPort");
                    writer.WriteValue(consensusNode.WebSocketPort);
                    writer.WriteEndObject();

                    writer.WritePropertyName("RPC");
                    writer.WriteStartObject();
                    writer.WritePropertyName("BindAddress");
                    writer.WriteValue("127.0.0.1");
                    writer.WritePropertyName("Port");
                    writer.WriteValue(consensusNode.RpcPort);
                    writer.WritePropertyName("SslCert");
                    writer.WriteValue("");
                    writer.WritePropertyName("SslCertPassword");
                    writer.WriteValue("");
                    writer.WriteEndObject();

                    writer.WritePropertyName("UnlockWallet");
                    writer.WriteStartObject();
                    writer.WritePropertyName("Path");
                    writer.WriteValue(walletPath);
                    writer.WritePropertyName("Password");
                    writer.WriteValue(password);
                    writer.WritePropertyName("StartConsensus");
                    writer.WriteValue(true);
                    writer.WritePropertyName("IsActive");
                    writer.WriteValue(true);
                    writer.WriteEndObject();

                    writer.WriteEndObject();
                    writer.WriteEndObject();

                }
            }

            {
                using (var stream = File.Open(Path.Combine(Directory.GetCurrentDirectory(), "protocol.json"), FileMode.Create, FileAccess.Write))
                using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("IsActive");
                    writer.WriteStartObject();

                    writer.WritePropertyName("IsActive");
                    writer.WriteValue(chain.Magic);
                    writer.WritePropertyName("IsActive");
                    writer.WriteValue(23);
                    writer.WritePropertyName("IsActive");
                    writer.WriteValue(15);

                    writer.WritePropertyName("StandbyValidators");
                    writer.WriteStartArray();
                    foreach (var conensusNode in chain.ConsensusNodes)
                    {
                        writer.WriteValue(conensusNode.Wallet.GetAccounts().Single(a => a.IsDefault).GetKey().PublicKey.EncodePoint(true).ToHexString());
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
                    writer.WriteEndObject();
                }
            }

            return 0;

        }
    }
}
