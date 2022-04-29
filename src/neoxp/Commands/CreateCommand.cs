using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.Wallets;
using NeoExpress.Models;
using static Neo.BlockchainToolkit.Constants;

namespace NeoExpress.Commands
{
    [Command("create", Description = "Create new neo-express instance")]
    internal class CreateCommand
    {
        readonly IFileSystem fileSystem;

        public CreateCommand(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        [Argument(0, Description = "name of " + EXPRESS_EXTENSION + " file to create (Default: ./" + DEFAULT_EXPRESS_FILENAME + ")")]
        internal string Output { get; set; } = string.Empty;

        [Option(Description = "Number of consensus nodes to create (Default: 1)")]
        [AllowedValues("1", "4", "7")]
        internal int Count { get; set; } = 1;

        [Option(Description = "Version to use for addresses in this blockchain instance (Default: 53)")]
        internal byte? AddressVersion { get; set; }

        [Option(Description = "Overwrite existing data")]
        internal bool Force { get; set; }

        internal ExpressChain CreateChain()
        {
            if (Count != 1 && Count != 4 && Count != 7)
            {
                throw new ArgumentException("invalid blockchain node count", nameof(Count));
            }

            var settings = ProtocolSettings.Default with
            {
                Network = ExpressChain.GenerateNetworkValue(),
                AddressVersion = AddressVersion ?? ProtocolSettings.Default.AddressVersion
            };

            var wallets = new List<(DevWallet wallet, WalletAccount account)>(Count);
            for (var i = 1; i <= Count; i++)
            {
                var wallet = new DevWallet(settings, $"node{i}");
                var account = wallet.CreateAccount();
                account.IsDefault = true;
                wallets.Add((wallet, account));
            }

            var keys = wallets.Select(t => t.account.GetKey().PublicKey).ToArray();
            var contract = Contract.CreateMultiSigContract((keys.Length * 2 / 3) + 1, keys);

            foreach (var (wallet, account) in wallets)
            {
                var multiSigContractAccount = wallet.CreateAccount(contract, account.GetKey());
                multiSigContractAccount.Label = "Consensus MultiSigContract";
            }

            var nodes = wallets.Select((w, i) => new ExpressConsensusNode
            {
                TcpPort = GetPortNumber(i, 3),
                WebSocketPort = GetPortNumber(i, 4),
                RpcPort = GetPortNumber(i, 2),
                Wallet = w.wallet.ToExpressWallet()
            });

            return new ExpressChain()
            {
                Network = settings.Network,
                AddressVersion = settings.AddressVersion,
                ConsensusNodes = nodes.ToList(),
            };

            // 49152 is the first port in the "Dynamic and/or Private" range as specified by IANA
            // http://www.iana.org/assignments/port-numbers
            static ushort GetPortNumber(int index, ushort portNumber) => (ushort)(50000 + ((index + 1) * 10) + portNumber);
        }

        internal void SaveChain(ExpressChain chain, string outputPath)
        {
            if (fileSystem.File.Exists(outputPath))
            {
                if (Force)
                {
                    fileSystem.File.Delete(outputPath);
                }
                else
                {
                    throw new Exception("You must specify --force to overwrite an existing file");
                }
            }

            if (fileSystem.File.Exists(outputPath))
            {
                throw new ArgumentException($"{outputPath} already exists", nameof(outputPath));
            }

            fileSystem.SaveChain(chain, outputPath);
        }

        internal void Execute(IConsole console)
        {
            var outputPath = fileSystem.ResolveExpressFileName(Output);
            if (fileSystem.File.Exists(outputPath) && !Force)
            {
                throw new Exception("You must specify --force to overwrite an existing file");
            }

            var chain = CreateChain();
            SaveChain(chain, outputPath);

            console.Out.WriteLine($"Created {chain.ConsensusNodes.Count} node privatenet at {outputPath}");
            console.Out.WriteLine("    Note: The private keys for the accounts in this file are are *not* encrypted.");
            console.Out.WriteLine("          Do not use these accounts on MainNet or in any other system where security is a concern.");
        }

        internal int OnExecute(CommandLineApplication app) => app.Execute(this.Execute);
    }
}
