using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    [Command("create", Description = "Create new neo-express instance")]
    internal class CreateCommand
    {
        [Argument(0, Description = "name of .neo-express file to create (Default: ./default.neo-express")]
        internal string Output { get; set; } = string.Empty;

        [Option(Description = "Number of consensus nodes to create\nDefault: 1")]
        [AllowedValues("1", "4", "7")]
        internal int Count { get; set; } = 1;

        [Option(Description = "Overwrite existing data")]
        internal bool Force { get; set; }

        internal int OnExecute(IFileSystem fileSystem, IConsole console)
        {
            try
            {
                var output = fileSystem.GetDefaultFilename(Output);
                if (fileSystem.File.Exists(output))
                {
                    if (Force)
                    {
                        fileSystem.File.Delete(output);
                    }
                    else
                    {
                        throw new Exception("You must specify --force to overwrite an existing file");
                    }
                }

                if (fileSystem.File.Exists(output))
                {
                    throw new ArgumentException($"{output} already exists", nameof(output));
                }

                if (Count != 1 && Count != 4 && Count != 7)
                {
                    throw new ArgumentException("invalid blockchain node count", nameof(Count));
                }

                var wallets = new List<(DevWallet wallet, Neo.Wallets.WalletAccount account)>(Count);

                for (var i = 1; i <= Count; i++)
                {
                    var wallet = new DevWallet($"node{i}");
                    var account = wallet.CreateAccount();
                    account.IsDefault = true;
                    wallets.Add((wallet, account));
                }

                var keys = wallets.Select(t => t.account.GetKey().PublicKey).ToArray();

                var contract = Neo.SmartContract.Contract.CreateMultiSigContract((keys.Length * 2 / 3) + 1, keys);

                foreach (var (wallet, account) in wallets)
                {
                    var multiSigContractAccount = wallet.CreateAccount(contract, account.GetKey());
                    multiSigContractAccount.Label = "MultiSigContract";
                }

                // 49152 is the first port in the "Dynamic and/or Private" range as specified by IANA
                // http://www.iana.org/assignments/port-numbers
                var nodes = new List<ExpressConsensusNode>(Count);
                for (var i = 0; i < Count; i++)
                {
                    nodes.Add(new ExpressConsensusNode()
                    {
                        TcpPort = GetPortNumber(i, 3),
                        WebSocketPort = GetPortNumber(i, 4),
                        RpcPort = GetPortNumber(i, 2),
                        Wallet = wallets[i].wallet.ToExpressWallet()
                    });
                }

                console.WriteLine($"Created {Count} node privatenet at {output}");
                console.WriteLine("    Note: The private keys for the accounts in this file are are *not* encrypted.");
                console.WriteLine("          Do not use these accounts on MainNet or in any other system where security is a concern.");

                var chain = new ExpressChain()
                {
                    Magic = ExpressChain.GenerateMagicValue(),
                    ConsensusNodes = nodes,
                };

                fileSystem.SaveChain(chain, output);

                return 0;
            }
            catch (Exception ex)
            {
                console.Error.WriteLine(ex.Message);
                return 1;
            }

            static ushort GetPortNumber(int index, ushort portNumber) => (ushort)(50000 + ((index + 1) * 10) + portNumber);
        }
    }
}
