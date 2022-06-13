using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Commands;
using NeoExpress.Models;
using NeoBctkUtility = Neo.BlockchainToolkit.Utility;

namespace NeoExpress.Node
{
    class NodeUtility
    {


        public static bool TryParseRpcUri(string value, [MaybeNullWhen(false)] out Uri uri)
        {
            if (value.Equals("mainnet", StringComparison.OrdinalIgnoreCase))
            {
                uri = new Uri("http://seed1.neo.org:10332");
                return true;
            }

            if (value.Equals("testnet", StringComparison.OrdinalIgnoreCase))
            {
                uri = new Uri("http://seed1t5.neo.org:20332");
                return true;
            }

            return (Uri.TryCreate(value, UriKind.Absolute, out uri)
                && uri is not null
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
        }



        const byte Prefix_Contract = 8;

        public static async Task<(ContractState contractState, IReadOnlyList<(string key, string value)> storagePairs)> DownloadContractStateAsync(
                string contractHash, string rpcUri, uint stateHeight)
        {
            if (!UInt160.TryParse(contractHash, out var _contractHash))
            {
                throw new ArgumentException($"Invalid contract hash: \"{contractHash}\"");
            }

            if (!TryParseRpcUri(rpcUri, out var uri))
            {
                throw new ArgumentException($"Invalid RpcUri value \"{rpcUri}\"");
            }

            using var rpcClient = new RpcClient(uri);
            var stateAPI = new StateAPI(rpcClient);

            if (stateHeight == 0)
            {
                uint? validatedRootIndex;
                try
                {
                    (_, validatedRootIndex) = await stateAPI.GetStateHeightAsync().ConfigureAwait(false);
                }
                catch (RpcException e) when (e.Message.Contains("Method not found"))
                {
                    throw new Exception(
                        "Could not get state information. Make sure the remote RPC server has state service support");
                }

                stateHeight = validatedRootIndex.HasValue ? validatedRootIndex.Value
                    : throw new Exception($"Null \"{nameof(validatedRootIndex)}\" in state height response");
            }

            var stateRoot = await stateAPI.GetStateRootAsync(stateHeight).ConfigureAwait(false);

            // rpcClient.GetContractStateAsync returns the current ContractState, but this method needs
            // the ContractState as it was at stateHeight. ContractManagement stores ContractState by
            // contractHash with the prefix 8. The following code uses stateAPI.GetStateAsync to retrieve
            // the value with that key at the height state root and then deserializes it into a ContractState
            // instance via GetInteroperable.

            var key = new byte[21];
            key[0] = Prefix_Contract;
            _contractHash.ToArray().CopyTo(key, 1);

            const int COR_E_KEYNOTFOUND = unchecked((int)0x80131577);
            ContractState contractState;
            try
            {
                var proof = await stateAPI.GetProofAsync(stateRoot.RootHash, NativeContract.ContractManagement.Hash, key)
                    .ConfigureAwait(false);
                var (_, value) = NeoBctkUtility.VerifyProof(stateRoot.RootHash, proof);
                var item = new StorageItem(value);
                contractState = item.GetInteroperable<ContractState>();
            }
            catch (RpcException ex) when (ex.HResult == COR_E_KEYNOTFOUND)
            {
                throw new Exception($"Contract {contractHash} not found at height {stateHeight}");
            }
            catch (RpcException ex) when (ex.HResult == -100 && ex.Message == "Unknown value")
            {
                // https://github.com/neo-project/neo-modules/pull/706
                throw new Exception($"Contract {contractHash} not found at height {stateHeight}");
            }

            if (contractState.Id < 0) throw new NotSupportedException("Contract download not supported for native contracts");

            var states = Enumerable.Empty<(string key, string value)>();
            ReadOnlyMemory<byte> start = default;

            while (true)
            {
                var @params = StateAPI.MakeFindStatesParams(stateRoot.RootHash, _contractHash, default, start.Span);
                var response = await rpcClient.RpcSendAsync("findstates", @params).ConfigureAwait(false);

                var results = (JArray)response["results"];
                if (results.Count == 0) break;

                ValidateProof(stateRoot.RootHash, response["firstProof"], results[0]);

                if (results.Count > 1)
                {
                    ValidateProof(stateRoot.RootHash, response["lastProof"], results[^1]);
                }

                states = states.Concat(results
                    .Select(j => (
                        j["key"].AsString(),
                        j["value"].AsString()
                    )));

                var truncated = response["truncated"].AsBoolean();
                if (!truncated) break;
                start = Convert.FromBase64String(results[^1]["key"].AsString());
            }

            return (contractState, states.ToList());

            static void ValidateProof(UInt256 rootHash, JObject proof, JObject result)
            {
                var proofBytes = Convert.FromBase64String(proof.AsString());
                var (provenKey, provenItem) = NeoBctkUtility.VerifyProof(rootHash, proofBytes);

                var key = Convert.FromBase64String(result["key"].AsString());
                if (!provenKey.Key.Span.SequenceEqual(key)) throw new Exception("Incorrect StorageKey");

                var value = Convert.FromBase64String(result["value"].AsString());
                if (!provenItem.AsSpan().SequenceEqual(value)) throw new Exception("Incorrect StorageItem");
            }
        }
    }
}
