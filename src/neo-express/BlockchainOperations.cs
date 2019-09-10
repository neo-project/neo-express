using NeoExpress.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

                return new ExpressChain()
                {
                    Magic = ExpressChain.GenerateMagicValue(),
                    ConsensusNodes = wallets.Select(t => new ExpressConsensusNode()
                    {
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
    }
}
