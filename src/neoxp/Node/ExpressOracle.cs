using System;
using System.Collections.Generic;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;

namespace NeoExpress.Node
{
    class ExpressOracle
    {
        public static void SignOracleResponseTransaction(ExpressChain chain, Transaction tx, ECPoint[] oracleNodes)
        {
            var signatures = new Dictionary<ECPoint, byte[]>();

            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var account = chain.ConsensusNodes[i].Wallet.DefaultAccount ?? throw new Exception("Invalid DefaultAccount");
                var key = DevWalletAccount.FromExpressWalletAccount(account).GetKey() ?? throw new Exception("Invalid KeyPair");
                if (oracleNodes.Contains(key.PublicKey))
                {
                    signatures.Add(key.PublicKey, tx.Sign(key));
                }
            }

            int m = oracleNodes.Length - (oracleNodes.Length - 1) / 3;
            if (signatures.Count < m)
            {
                throw new Exception("Insufficient oracle response signatures");
            }

            var contract = Contract.CreateMultiSigContract(m, oracleNodes);
            var sb = new ScriptBuilder();
            foreach (var kvp in signatures.OrderBy(p => p.Key).Take(m))
            {
                sb.EmitPush(kvp.Value);
            }
            var index = tx.GetScriptHashesForVerifying(null)[0] == contract.ScriptHash ? 0 : 1;
            tx.Witnesses[index].InvocationScript = sb.ToArray();
        }
    }
}
