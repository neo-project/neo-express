using McMaster.Extensions.CommandLineUtils;
using System.IO;
using System.Linq;

namespace Neo.Express.Commands
{
    [Command("export")]
    class ExportCommand
    {
        [Option]
        string Input { get; }

        public static DevChain LoadDevChain(string filePath)
        {
            //using (var stream = System.IO.File.OpenRead(filePath))
            //{
            //    var doc = JsonDocument.Parse(stream);
            //    return DevChain.Parse(doc);
            //}
            return null;
        }

        int OnExecute(CommandLineApplication app, IConsole console)
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

            var chain = LoadDevChain(input);

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

                //using (var stream = File.Open(Path.Combine(Directory.GetCurrentDirectory(), $"{consensusNode.Wallet.Name}.config.json"), FileMode.Create, FileAccess.Write))
                //using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                //{
                //    writer.WriteStartObject();
                //    writer.WriteStartObject("ApplicationConfiguration");

                //    writer.WriteStartObject("Paths");
                //    writer.WriteString("Chain", "Chain_{0}");
                //    writer.WriteEndObject();

                //    writer.WriteStartObject("P2P");
                //    writer.WriteNumber("Port", consensusNode.TcpPort);
                //    writer.WriteNumber("WsPort", consensusNode.WebSocketPort);
                //    writer.WriteEndObject();

                //    writer.WriteStartObject("RPC");
                //    writer.WriteString("BindAddress", "127.0.0.1");
                //    writer.WriteNumber("Port", consensusNode.RpcPort);
                //    writer.WriteString("SslCert", "");
                //    writer.WriteString("SslCertPassword", "");
                //    writer.WriteEndObject();

                //    writer.WriteStartObject("UnlockWallet");
                //    writer.WriteString("Path", walletPath);
                //    writer.WriteString("Password", password);
                //    writer.WriteBoolean("StartConsensus", true);
                //    writer.WriteBoolean("IsActive", true);
                //    writer.WriteEndObject();

                //    writer.WriteEndObject();
                //    writer.WriteEndObject();
                //}
            }

            {
                //using (var stream = File.Open(Path.Combine(Directory.GetCurrentDirectory(), "protocol.json"), FileMode.Create, FileAccess.Write))
                //using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                //{
                //    writer.WriteStartObject();
                //    writer.WriteStartObject("ProtocolConfiguration");

                //    writer.WriteNumber("Magic", chain.Magic);
                //    writer.WriteNumber("AddressVersion", 23);
                //    writer.WriteNumber("SecondsPerBlock", 15);

                //    writer.WriteStartArray("StandbyValidators");
                //    foreach (var conensusNode in chain.ConsensusNodes)
                //    {
                //        writer.WriteStringValue(conensusNode.Wallet.GetAccounts().Single(a => a.IsDefault).GetKey().PublicKey.EncodePoint(true).ToHexString());
                //    }
                //    writer.WriteEndArray();

                //    writer.WriteStartArray("SeedList");
                //    foreach (var node in chain.ConsensusNodes)
                //    {
                //        writer.WriteStringValue($"{System.Net.IPAddress.Loopback}:{node.TcpPort}");
                //    }
                //    writer.WriteEndArray();

                //    writer.WriteEndObject();
                //    writer.WriteEndObject();
                //}
            }

            return 0;

        }
    }
}
