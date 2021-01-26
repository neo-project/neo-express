using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.Network.RPC;
using Neo.SmartContract.Native;
using Neo.Wallets;
using NeoExpress.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NeoExpress
{
    static class OldExtensions
    {
        public static string ToHexString(this byte[] value, bool reverse = false)
        {
            var sb = new System.Text.StringBuilder();

            if (reverse)
            {
                for (int i = value.Length - 1; i >= 0; i--)
                {
                    sb.AppendFormat("{0:x2}", value[i]);
                }
            }
            else
            {
                for (int i = 0; i < value.Length; i++)
                {
                    sb.AppendFormat("{0:x2}", value[i]);
                }
            }
            return sb.ToString();
        }

        public static byte[] ToByteArray(this string value)
        {
            if (value == null || value.Length == 0)
                return new byte[0];
            if (value.Length % 2 == 1)
                throw new FormatException();
            byte[] result = new byte[value.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = byte.Parse(value.Substring(i * 2, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
            return result;
        }

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
            => GetUri(chain.ConsensusNodes[node]);

        public static Uri GetUri(this ExpressConsensusNode node)
            => new Uri($"http://localhost:{node.RpcPort}");

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
                var signers = chain.GetMultiSigAccounts(account);

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
        }

        public static IEnumerable<DevWallet> GetMultiSigWallets(this ExpressChain chain, ExpressWalletAccount account)
            => chain.ConsensusNodes
                .Select(n => n.Wallet)
                .Concat(chain.Wallets)
                .Where(w => w.Accounts.Find(a => a.ScriptHash == account.ScriptHash) != null)
                .Select(Models.DevWallet.FromExpressWallet);

        public static IEnumerable<DevWalletAccount> GetMultiSigAccounts(this ExpressChain chain, ExpressWalletAccount account)
            => chain.ConsensusNodes
                .Select(n => n.Wallet)
                .Concat(chain.Wallets)
                .Select(w => w.Accounts.Find(a => a.ScriptHash == account.ScriptHash))
                .Where(a => a != null)
                .Select(DevWalletAccount.FromExpressWalletAccount!);

        public static TransactionManager AddGas(this TransactionManager transactionManager, decimal gas)
        {
            if (transactionManager.Tx != null && gas > 0.0m)
            {
                transactionManager.Tx.SystemFee += (long)gas.ToBigInteger(NativeContract.GAS.Decimals);
            }
            return transactionManager;
        }

        public static IExpressNode GetExpressNode(this ExpressChain chain, bool offlineTrace = false)
        {
            throw new NotImplementedException();
            // if (chain.IsRunning(out var node))
            // {
            //     return new Node.OnlineNode(node);
            // }

            // node = chain.ConsensusNodes[0];
            // var folder = node.GetBlockchainPath();
            // if (!Directory.Exists(folder))
            // {
            //     Directory.CreateDirectory(folder);
            // }
            // return new Node.OfflineNode(RocksDbStore.Open(folder), node.Wallet, chain, offlineTrace);
        }
    }
}
