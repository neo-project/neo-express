using System;
using System.IO;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
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
            readonly IExpressChain chain;

            public List(IExpressChain chain)
            {
                this.chain = chain;
            }

            public List(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal int OnExecute(CommandLineApplication app) => app.Execute(this.Execute);

            internal void Execute(IConsole console)
            {
                var consensusScriptHash = chain.GetConsensusContract().ScriptHash;
                var consensusAddress = Neo.Wallets.Helper.ToAddress(consensusScriptHash, chain.AddressVersion);

                if (Json)
                {
                    using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                    using var _ = writer.WriteObject();

                    writer.WritePropertyName(IExpressChain.GENESIS);
                    using (var __ = writer.WriteObject())
                    {
                        writer.WriteProperty("account-label", IExpressChain.GENESIS);
                        writer.WriteProperty("address", consensusAddress);
                        writer.WriteProperty("script-hash", consensusScriptHash.ToString());
                    }

                    foreach (var node in chain.ConsensusNodes)
                    {
                        writer.WriteWallet(node.Wallet);
                    }

                    foreach (var wallet in chain.Wallets)
                    {
                        writer.WriteWallet(wallet);
                    }
                }
                else
                {
                    var genesisScriptHash = Neo.IO.Helper.ToArray(consensusScriptHash);

                    console.Out.WriteLine(IExpressChain.GENESIS);
                    console.Out.WriteLine($"  {consensusAddress}");
                    console.Out.WriteLine($"    script hash: {BitConverter.ToString(genesisScriptHash)}");

                    foreach (var node in chain.ConsensusNodes)
                    {
                        console.Out.WriteWallet(node.Wallet);
                    }

                    foreach (var wallet in chain.Wallets)
                    {
                        console.Out.WriteWallet(wallet);
                    }
                }
            }
        }
    }
}
