using Neo.Express.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.Express.Backend2
{
    public class Neo2Backend : INeoBackend
    {
        public void CreateBlockchain(string filename, int count, ushort port)
        {
            if ((uint)port + (count * 3) >= ushort.MaxValue)
            {
                // TODO: better error message
                throw new Exception("Invalid port");
            }

            var wallets = new List<(DevWallet wallet, Wallets.WalletAccount account)>(count);

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

            var chain = new DevChain(wallets.Select(t => new DevConsensusNode()
            {
                Wallet = t.wallet,
                TcpPort = port++,
                WebSocketPort = port++,
                RpcPort = port++
            }));

            chain.Save(filename);
        }
    }
}
