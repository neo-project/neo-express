using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.SmartContract;
using Neo.Wallets;
using NeoExpress.Models;
using Nito.Disposables;

namespace NeoExpress
{

    static class ExpressChainExtensions
    {
        public static IExpressNode GetExpressNode(this Neo.BlockchainToolkit.Models.ExpressChain chain, IFileSystem fileSystem, bool offlineTrace = false)
        {
            throw new NotImplementedException();
        }


        public static Contract CreateGenesisContract(this Neo.BlockchainToolkit.Models.ExpressChain chain)
        {
            List<Neo.Cryptography.ECC.ECPoint> publicKeys = new(chain.ConsensusNodes.Count);
            foreach (var node in chain.ConsensusNodes)
            {
                var account = node.Wallet.DefaultAccount ?? throw new Exception("Missing Default Account");
                var privateKey = Convert.FromHexString(account.PrivateKey);
                var keyPair = new KeyPair(privateKey);
                publicKeys.Add(keyPair.PublicKey);
            }

            var m = publicKeys.Count * 2 / 3 + 1;
            return Contract.CreateMultiSigContract(m, publicKeys);
        }



        public static UInt160 GetScriptHash(this ExpressWalletAccount? @this)
        {
            ArgumentNullException.ThrowIfNull(@this);

            var keyPair = new KeyPair(@this.PrivateKey.HexToBytes());
            var contract = Neo.SmartContract.Contract.CreateSignatureContract(keyPair.PublicKey);
            return contract.ScriptHash;
        }








        public static bool IsMultiSigContract(this WalletAccount @this)
            => Neo.SmartContract.Helper.IsMultiSigContract(@this.Contract.Script);

        public static IEnumerable<WalletAccount> GetMultiSigAccounts(this Wallet wallet) => wallet.GetAccounts().Where(IsMultiSigContract);



        internal const string GENESIS = "genesis";



    }
}
