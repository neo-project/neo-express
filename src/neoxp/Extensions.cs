using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract.Native;
using NeoExpress.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NeoExpress
{
    static class Extensions
    {
        public static BigDecimal ToBigDecimal(this RpcNep17Balance balance, byte decimals)
            => new BigDecimal(balance.Amount, decimals);

        public static UInt160 ParseScriptHash(this ContractParameterParser parser, string hashOrContract)
        {
            if (UInt160.TryParse(hashOrContract, out var hash))
            {
                return hash;
            }

            if (parser.TryLoadScriptHash(hashOrContract, out var value))
            {
                return value;
            }

            throw new ArgumentException(nameof(hashOrContract));
        }

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
