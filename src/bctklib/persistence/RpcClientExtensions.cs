// Copyright (C) 2015-2024 The Neo Project.
//
// RpcClientExtensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.RPC;
using Neo.Network.RPC.Models;

namespace Neo.BlockchainToolkit.Persistence
{
    static class RpcClientExtensions
    {
        // // TODO: remove when https://github.com/neo-project/neo-modules/issues/756 is resolved
        // internal static async Task<UInt256> GetBlockHashAsync(this RpcClient rpcClient, uint index)
        // {
        //     var result = await rpcClient.RpcSendAsync("getblockhash", index).ConfigureAwait(false);
        //     return UInt256.Parse(result.AsString());
        // }

        internal static byte[] GetProof(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
        {
            var result = rpcClient.RpcSend(
                RpcClient.GetRpcName(),
                rootHash.ToString(),
                scriptHash.ToString(),
                Convert.ToBase64String(key));
            return Convert.FromBase64String(result.AsString());
        }

        internal static byte[]? GetProvenState(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> key)
        {
            const int COR_E_KEYNOTFOUND = unchecked((int)0x80131577);

            try
            {
                var result = rpcClient.GetProof(rootHash, scriptHash, key);
                return Utility.VerifyProof(rootHash, result).value;
            }
            // GetProvenState has to match the semantics of IReadOnlyStore.TryGet
            // which returns null for invalid keys instead of throwing an exception.
            catch (RpcException ex) when (ex.HResult == COR_E_KEYNOTFOUND)
            {
                // Trie class throws KeyNotFoundException if key is not in the trie.
                // RpcClient/Server converts the KeyNotFoundException into an
                // RpcException with code == COR_E_KEYNOTFOUND.

                return null;
            }
            catch (RpcException ex) when (ex.HResult == -100 && ex.Message == "Unknown value")
            {
                // Prior to Neo 3.3.0, StateService GetProof method threw a custom exception 
                // instead of KeyNotFoundException like GetState. This catch clause detected
                // the custom exception that GetProof used to throw. 

                // TODO: remove this clause once deployed StateService for Neo N3 MainNet and
                //       TestNet has been verified to be running Neo 3.3.0 or later.

                return null;
            }
        }

        internal static byte[] GetStorage(this RpcClient rpcClient, UInt160 contractHash, ReadOnlySpan<byte> key)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), contractHash.ToString(), Convert.ToBase64String(key));
            return Convert.FromBase64String(result.AsString());
        }

        internal static RpcStateRoot GetStateRoot(this RpcClient rpcClient, uint index)
        {
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), index);
            return RpcStateRoot.FromJson((Json.JObject)result);
        }

        internal static async Task<RpcStateRoot> GetStateRootAsync(this RpcClient rpcClient, uint index)
        {
            var result = await rpcClient.RpcSendAsync(RpcClient.GetRpcName(), index).ConfigureAwait(false);
            return RpcStateRoot.FromJson((Json.JObject)result);
        }

        internal static RpcFoundStates FindStates(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> from = default, int? count = null)
        {
            var @params = StateAPI.MakeFindStatesParams(rootHash, scriptHash, prefix, from, count);
            var result = rpcClient.RpcSend(RpcClient.GetRpcName(), @params);
            return RpcFoundStates.FromJson((Json.JObject)result);
        }

        internal static async Task<RpcFoundStates> FindStatesAsync(this RpcClient rpcClient, UInt256 rootHash, UInt160 scriptHash, ReadOnlyMemory<byte> prefix, ReadOnlyMemory<byte> from = default, int? count = null)
        {
            var @params = StateAPI.MakeFindStatesParams(rootHash, scriptHash, prefix.Span, from.Span, count);
            var result = await rpcClient.RpcSendAsync(RpcClient.GetRpcName(), @params).ConfigureAwait(false);
            return RpcFoundStates.FromJson((Json.JObject)result);
        }
    }
}
