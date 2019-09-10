using Neo;
using NeoExpress.Models;
using NeoExpress.Node;
using NeoExpress.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace NeoExpress
{
    internal static class BlockchainOperations
    {
        public static ExpressChain CreateBlockchain(int count)
        {
            var wallets = new List<(DevWallet wallet, Neo.Wallets.WalletAccount account)>(count);

            try
            {
                for (int i = 1; i <= count; i++)
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
                ushort port = 49152;
                return new ExpressChain()
                {
                    Magic = ExpressChain.GenerateMagicValue(),
                    ConsensusNodes = wallets.Select(t => new ExpressConsensusNode()
                    {
                        TcpPort = port++,
                        WebSocketPort = port++,
                        RpcPort = port++,
                        Wallet = t.wallet.ToExpressWallet()
                    }).ToList()
                };
            }
            finally
            {
                foreach (var (wallet, _) in wallets)
                {
                    wallet.Dispose();
                }
            }
        }

        public static ExpressWallet CreateWallet(string name)
        {
            using (var wallet = new DevWallet(name))
            {
                var account = wallet.CreateAccount();
                account.IsDefault = true;
                return wallet.ToExpressWallet();
            }
        }

        public static void ExportWallet(ExpressWallet wallet, string filename, string password)
        {
            var devWallet = DevWallet.FromExpressWallet(wallet);
            devWallet.Export(filename, password);
        }

        public static CancellationTokenSource RunBlockchain(string directory, ExpressChain chain, int index, uint secondsPerBlock, TextWriter writer, ushort startingPort = 0)
        {
            if (startingPort != 0)
            {
                foreach (var consensusNode in chain.ConsensusNodes)
                {

                    consensusNode.TcpPort = startingPort++;
                    consensusNode.WebSocketPort = startingPort++;
                    consensusNode.RpcPort = startingPort++;
                }
            }

            chain.InitializeProtocolSettings(secondsPerBlock);

            var node = chain.ConsensusNodes[index];

#pragma warning disable IDE0067 // Dispose objects before losing scope
            // NodeUtility.Run disposes the store when it's done
            return NodeUtility.Run(new RocksDbStore(directory), node, writer);
#pragma warning restore IDE0067 // Dispose objects before losing scope
        }
    }
}
