using Neo.Express.Abstractions;
using Neo.Express.Backend2.Persistence;
using Neo.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Express.Backend2
{
    public partial class Neo2Backend : INeoBackend
    {
        //public static string ROOT_PATH => Path.Combine(
        //    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        //    "NEO-Express", "backend2", "blockchain-nodes");

        public ExpressChain CreateBlockchain(int count, ushort port)
        {
            if ((uint)port + (count * 3) >= ushort.MaxValue)
            {
                // TODO: better error message
                throw new Exception("Invalid port");
            }

            var wallets = new List<(DevWallet wallet, Wallets.WalletAccount account)>(count);

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

        public ExpressWallet CreateWallet(string name)
        {
            using (var wallet = new DevWallet(name))
            {
                var account = wallet.CreateAccount();
                account.IsDefault = true;
                return wallet.ToExpressWallet();
            }
        }

        private static CancellationTokenSource Run(Store store, ExpressConsensusNode node, Action<string> writeConsole)
        {
            var cts = new CancellationTokenSource();

            Task.Factory.StartNew(() =>
            {
                try
                {
                    var wallet = DevWallet.FromExpressWallet(node.Wallet);
                    using (var system = new NeoSystem(store))
                    {
                        var logPlugin = new LogPlugin(writeConsole);
                        var rpcPlugin = new ExpressNodeRpcPlugin();

                        system.StartNode(node.TcpPort, node.WebSocketPort);
                        system.StartConsensus(wallet);
                        system.StartRpc(IPAddress.Any, node.RpcPort, wallet);

                        cts.Token.WaitHandle.WaitOne();
                    }
                }
                catch (Exception ex)
                {
                    writeConsole(ex.ToString());
                    cts.Cancel();
                }
                finally
                {
                    if (store is IDisposable disp)
                    {
                        disp.Dispose();
                    }
                }
            });

            return cts;
        }

        public CancellationTokenSource RunBlockchain(string folder, ExpressChain chain, int index, uint secondsPerBlock, Action<string> writeConsole)
        {
            if (index >= chain.ConsensusNodes.Count || index < 0)
            {
                throw new Exception("Invalid node index");
            }

            chain.InitializeProtocolSettings(secondsPerBlock);

            var node = chain.ConsensusNodes[index];

            return Run(new RocksDbStore(folder), node, writeConsole);
        }
    }
}
