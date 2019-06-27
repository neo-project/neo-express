using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Neo.Express.Commands
{
    [Command("export")]
    class ExportCommand
    {
        [Option]
        string Input { get; }

        public static DevChain LoadDevChain(string filePath)
        {
            using (var stream = System.IO.File.OpenRead(filePath))
            {
                var doc = JsonDocument.Parse(stream);
                return DevChain.Parse(doc);
            }
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

            for (var i = 0; i < chain.ConensusNodes.Count; i++)
            {
                var conensusNode = chain.ConensusNodes[i];
                console.WriteLine($"Exporting {conensusNode.Name} Conensus Node wallet");

                var walletPath = Path.Combine(Directory.GetCurrentDirectory(), $"{conensusNode.Name}.wallet.json");
                if (File.Exists(walletPath))
                {
                    File.Delete(walletPath);
                }

                var nep6Wallet = new Neo.Wallets.NEP6.NEP6Wallet(walletPath, conensusNode.Name);
                nep6Wallet.Unlock(password);
                foreach (var account in conensusNode.GetAccounts())
                {
                    nep6Wallet.CreateAccount(account.Contract, account.GetKey());
                }
                nep6Wallet.Save();

                var basePort = (i + 1) * 10000;
                using (var stream = File.Open(Path.Combine(Directory.GetCurrentDirectory(), $"{conensusNode.Name}.config.json"), FileMode.Create, FileAccess.Write))
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteStartObject("ApplicationConfiguration");

                    writer.WriteStartObject("Paths");
                    writer.WriteString("Chain", "Chain_{0}");
                    writer.WriteEndObject();

                    writer.WriteStartObject("P2P");
                    writer.WriteNumber("Port", basePort + 1);
                    writer.WriteNumber("WsPort", basePort + 2);
                    writer.WriteEndObject();

                    writer.WriteStartObject("RPC");
                    writer.WriteString("BindAddress", "127.0.0.1");
                    writer.WriteNumber("Port", basePort + 3);
                    writer.WriteString("SslCert", "");
                    writer.WriteString("SslCertPassword", "");
                    writer.WriteEndObject();

                    writer.WriteStartObject("UnlockWallet");
                    writer.WriteString("Path", walletPath);
                    writer.WriteString("Password", password);
                    writer.WriteBoolean("StartConsensus", true);
                    writer.WriteBoolean("IsActive", true);
                    writer.WriteEndObject();

                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
            }

            {
                using (var stream = File.Open(Path.Combine(Directory.GetCurrentDirectory(), "protocol.json"), FileMode.Create, FileAccess.Write))
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteStartObject("ProtocolConfiguration");

                    writer.WriteNumber("Magic", chain.Magic);
                    writer.WriteNumber("AddressVersion", 23);
                    writer.WriteNumber("SecondsPerBlock", 15);

                    writer.WriteStartArray("StandbyValidators");
                    foreach (var wallet in chain.ConensusNodes)
                    {
                        writer.WriteStringValue(wallet.GetAccounts().Single(a => a.IsDefault).GetKey().PublicKey.EncodePoint(true).ToHexString());
                    }
                    writer.WriteEndArray();

                    writer.WriteStartArray("SeedList");
                    for (var i = 0; i < chain.ConensusNodes.Count; i++)
                    {
                        writer.WriteStringValue($"{System.Net.IPAddress.Loopback}:{((i + 1) * 10000) + 1}");
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
