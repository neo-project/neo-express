﻿using Neo;
using Neo.Network.RPC;
using Neo.SmartContract.Native;
using Neo.Wallets;
using NeoExpress.Abstractions;
using NeoExpress.Abstractions.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NeoExpress.Neo3
{
    static class Extensions
    {
        public static void WriteResult(this TextWriter writer, JToken? result)
        {
            if (result != null)
            {
                writer.WriteLine(result.ToString(Formatting.Indented));
            }
            else
            {
                writer.WriteLine("<no result provided>");
            }
        }

        public static Uri GetUri(this ExpressChain chain, int node = 0)
            => new Uri($"http://localhost:{chain.ConsensusNodes[node].RpcPort}");

        static string ROOT_PATH => Path.Combine(
             Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
             "Neo-Express", "blockchain-nodes");

        static string GetBlockchainPath(this ExpressWalletAccount account)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            return Path.Combine(ROOT_PATH, account.ScriptHash);
        }

        static string GetBlockchainPath(this ExpressWallet wallet)
        {
            if (wallet == null)
            {
                throw new ArgumentNullException(nameof(wallet));
            }

            return wallet.Accounts
                .Single(a => a.IsDefault)
                .GetBlockchainPath();
        }

        public static string GetBlockchainPath(this ExpressConsensusNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            return node.Wallet.GetBlockchainPath();
        }

        public static bool IsMultiSigContract(this ExpressWalletAccount account)
            => Neo.SmartContract.Helper.IsMultiSigContract(account.Contract.Script.ToByteArray());

        public static bool IsMultiSigContract(this Neo.Wallets.WalletAccount account)
            => Neo.SmartContract.Helper.IsMultiSigContract(account.Contract.Script);

        public static async Task<Neo.IO.Json.JObject> RpcSendAsync(this RpcClient @this, string method, params Neo.IO.Json.JObject[] paraArgs)
        {
            var request = new Neo.Network.RPC.Models.RpcRequest
            {
                Id = 1,
                JsonRpc = "2.0",
                Method = method,
                Params = paraArgs
            };

            var response = await @this.SendAsync(request).ConfigureAwait(false);
            return response.Result;
        }

        public static KeyPair GetKey(this ExpressWalletAccount account) => new KeyPair(account.PrivateKey.HexToBytes());

        public static UInt160 GetScriptHashAsUInt160(this ExpressWalletAccount account) => account.ScriptHash.ToScriptHash();

        public static TransactionManager AddSignatures(this TransactionManager tm, ExpressChain chain, ExpressWalletAccount account)
        {
            if (account.IsMultiSigContract())
            {
                var signers = GetMultiSigAccounts();

                var publicKeys = signers.Select(s => s.GetKey()!.PublicKey).ToArray();
                var sigCount = account.Contract.Parameters.Count;

                foreach (var signer in signers.Take(sigCount))
                {
                    var keyPair = signer.GetKey() ?? throw new Exception();
                    tm = tm.AddMultiSig(keyPair, sigCount, publicKeys);
                }

                return tm;
            }
            else
            {
                return tm.AddSignature(account.GetKey()!);
            }

            IEnumerable<WalletAccount> GetMultiSigAccounts()
            {
                return chain.ConsensusNodes
                    .Select(n => n.Wallet)
                    .Concat(chain.Wallets)
                    .Select(w => w.Accounts.Find(a => a.ScriptHash == account.ScriptHash))
                    .Where(a => a != null)
                    .Select(Models.DevWalletAccount.FromExpressWalletAccount);
            }
        }

        public static TransactionManager AddGas(this TransactionManager transactionManager, decimal gas)
        {
            if (transactionManager.Tx != null && gas > 0.0m)
            {
                transactionManager.Tx.SystemFee += (long)gas.ToBigInteger(NativeContract.GAS.Decimals);
            }
            return transactionManager;
        }
    }
}
