using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Neo;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;

namespace NeoExpress.Neo3
{
    static class RpcClientExtensions
    {
        public static Task<UInt256> SendRawTransactionAsync(this RpcClient @this, Transaction transaction)
        {
            return @this.SendRawTransactionAsync(transaction.ToArray());
        }

        public static async Task<UInt256> SendRawTransactionAsync(this RpcClient @this, byte[] rawTransaction)
        {
            var result = await @this.RpcSendAsync("sendrawtransaction", rawTransaction.ToHexString()).ConfigureAwait(false);
            return UInt256.Parse(result["hash"].AsString());
        }

        public static async Task<BigInteger> BalanceOfAsync(this RpcClient @this, UInt160 scriptHash, UInt160 account)
        {
            var result = await @this.TestInvokeAsync(scriptHash, "balanceOf", account).ConfigureAwait(false);
            return result.Stack.Single().ToStackItem().GetBigInteger();
        }

        public static async Task<byte> DecimalsAsync(this RpcClient @this, UInt160 scriptHash)
        {
            var result = await @this.TestInvokeAsync(scriptHash, "decimals").ConfigureAwait(false);
            return (byte)result.Stack.Single().ToStackItem().GetBigInteger();
        }

        public static async Task<uint> GetBlockCountAsync(this RpcClient @this)
        {
            var result = await @this.RpcSendAsync("getblockcount").ConfigureAwait(false);
            return (uint)result.AsNumber();
        }

        public static async Task<long> GetFeePerByteAsync(this RpcClient @this)
        {
            var result = await @this.TestInvokeAsync(NativeContract.Policy.Hash, "getFeePerByte").ConfigureAwait(false);
            return (long)result.Stack.Single().ToStackItem().GetBigInteger();
        }

        public static Task<ContractState> GetContractStateAsync(this RpcClient @this, UInt160 hash)
        {
            return @this.GetContractStateAsync(hash.ToString());
        }

        public static async Task<ContractState> GetContractStateAsync(this RpcClient @this, string hash)
        {
            var result = await @this.RpcSendAsync("getcontractstate", hash).ConfigureAwait(false);
            return RpcContractState.FromJson(result).ContractState;
        }
        
        public static Task<RpcInvokeResult> TestInvokeAsync(this RpcClient @this, UInt160 scriptHash, string operation, params object[] args)
        {
            byte[] script = scriptHash.MakeScript(operation, args);
            return @this.InvokeScriptAsync(script);
        }
        
        public static async Task<RpcInvokeResult> InvokeScriptAsync(this RpcClient @this, byte[] script, params UInt160[] scriptHashesForVerifying)
        {
            List<JObject> parameters = new List<JObject>
            {
                script.ToHexString()
            };
            parameters.AddRange(scriptHashesForVerifying.Select(p => (JObject)p.ToString()));
            var result = await @this.RpcSendAsync("invokescript", parameters.ToArray()).ConfigureAwait(false);
            return RpcInvokeResult.FromJson(result);
        }

        public static async Task<JObject> RpcSendAsync(this RpcClient @this, string method, params JObject[] paraArgs)
        {
            var request = new RpcRequest
            {
                Id = 1,
                JsonRpc = "2.0",
                Method = method,
                Params = paraArgs
            };

            var result = await @this.SendAsync(request).ConfigureAwait(false);
            return result.Result;
        }
    }
}
