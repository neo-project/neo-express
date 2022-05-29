using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.IO;
using NeoExpress.Models;
using Newtonsoft.Json;
using Nito.Disposables;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("list", Description = "List neo-express wallets")]
        internal class List
        {
            readonly IExpressChain expressFile;

            public List(IExpressChain expressFile)
            {
                this.expressFile = expressFile;
            }

            public List(CommandLineApplication app) : this(app.GetExpressFile())
            {
            }

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal int OnExecute(CommandLineApplication app) => app.Execute(this.Execute);

            internal void Execute(IConsole console)
            {
                // TODO: expressFile.Chain.GetProtocolSettings
                var settings = expressFile.Chain.GetProtocolSettings();

                // TODO: expressFile.Chain.CreateGenesisContract
                var genesisContract = expressFile.Chain.CreateGenesisContract();
                var genesisAddress = Neo.Wallets.Helper.ToAddress(genesisContract.ScriptHash, expressFile.AddressVersion);

                if (Json)
                {
                    using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                    using var _ = writer.WriteObject();

                    writer.WritePropertyName(ExpressChainExtensions.GENESIS);
                    using (var __ = writer.WriteObject())
                    {
                        writer.WriteProperty("account-label", ExpressChainExtensions.GENESIS);
                        writer.WriteProperty("address", genesisAddress);
                        writer.WriteProperty("script-hash", genesisContract.ScriptHash.ToString());
                    }

                    foreach (var node in expressFile.ConsensusNodes)
                    {
                        writer.WriteWallet(node.Wallet);
                    }

                    foreach (var wallet in expressFile.Wallets)
                    {
                        writer.WriteWallet(wallet);
                    }
                }
                else
                {
                    var genesisScriptHash = Neo.IO.Helper.ToArray(genesisContract.ScriptHash);

                    console.Out.WriteLine(ExpressChainExtensions.GENESIS);
                    console.Out.WriteLine($"  {genesisAddress}");
                    console.Out.WriteLine($"    script hash: {BitConverter.ToString(genesisScriptHash)}");

                    foreach (var node in expressFile.ConsensusNodes)
                    {
                        console.Out.WriteWallet(node.Wallet);
                    }

                    foreach (var wallet in expressFile.Wallets)
                    {
                        console.Out.WriteWallet(wallet);
                    }
                }
            }
        }
    }
}
