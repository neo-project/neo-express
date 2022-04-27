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
            readonly IFileSystem fileSystem;

            public List(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = fileSystem.LoadExpressChain(Input);
                    var settings = chain.GetProtocolSettings();

                    var genesis = chain.GetGenesisAccount(settings);
                    var genesisAccount = chain.ConsensusNodes[0].Wallet.Accounts.Single(a => a.IsMultiSigContract());

                    if (Json)
                    {
                        using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                        using var _ = writer.WriteStartObjectAuto();

                        writer.WritePropertyName(ExpressChainExtensions.GENESIS);
                        writer.WriteAccount(genesisAccount, ExpressChainExtensions.GENESIS);

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
                        console.Out.WriteLine(ExpressChainExtensions.GENESIS);
                        console.Out.WriteAccount(genesisAccount);

                        foreach (var node in chain.ConsensusNodes)
                        {
                            console.Out.WriteWallet(node.Wallet);
                        }

                        foreach (var wallet in chain.Wallets)
                        {
                            console.Out.WriteWallet(wallet);
                        }
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }
        }
    }
}
