using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;

namespace NeoExpress
{
    static class Extensions
    {
        public static IReadOnlyList<T> ToReadOnlyList<T>(this IEnumerable<T> @this) => @this.ToList();

        // TODO: Remove when https://github.com/neo-project/neo-modules/issues/720 is fixed
        public static async Task<RpcInvokeResult> InvokeScriptAsync(this RpcClient rpcClient, Script script, params Signer[] signers)
        {
            List<Neo.IO.Json.JObject> list = new List<Neo.IO.Json.JObject> { Convert.ToBase64String(script.AsSpan()) };
            if (signers.Length != 0)
            {
                list.Add(signers.Select((Signer p) => p.ToJson()).ToArray());
            }
            var result = await rpcClient.RpcSendAsync("invokescript", list.ToArray()).ConfigureAwait(false);
            return RpcInvokeResult.FromJson(result);
        }
    }
}
