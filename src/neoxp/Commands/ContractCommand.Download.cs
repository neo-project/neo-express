using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using NeoExpress.Node;
using TextWriter = System.IO.TextWriter;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        internal enum OverwriteForce
        {
            None,
            All,
            ContractOnly,
            StorageOnly
        }

        [Command(Name = "download", Description = "Download contract with storage from remote chain into local chain")]
        internal class Download
        {
            readonly IExpressChain chain;

            public Download(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Download(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Contract invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Argument(1, Description = "URL of Neo JSON-RPC Node\nSpecify MainNet (default), TestNet or JSON-RPC URL")]
            internal string RpcUri { get; } = string.Empty;

            [Option(Description = "Block height to get contract state for")]
            internal uint Height { get; } = 0;

            [Option(CommandOptionType.SingleOrNoValue,
                Description = "Replace contract and storage if it already exists (Default: All)")]
            [AllowedValues(StringComparison.OrdinalIgnoreCase, "All", "ContractOnly", "StorageOnly")]
            internal OverwriteForce Force { get; init; } = OverwriteForce.None;

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Contract download is only supported for single-node consensus");
                }
                using var expressNode = chain.GetExpressNode();
                await ExecuteAsync(expressNode, Contract, RpcUri, Height, Force, console.Out).ConfigureAwait(false);
            }

            public static async Task ExecuteAsync(IExpressNode expressNode, string contract, string rpcUri, uint height, OverwriteForce force, TextWriter? writer = null)
            {
                var (state, storage) = await DownloadContractStateAsync(contract, rpcUri, height)
                    .ConfigureAwait(false);

                await expressNode.PersistContractAsync(state, storage, force).ConfigureAwait(false);
                writer?.WriteLineAsync($"{contract} downloaded from {rpcUri}").ConfigureAwait(false);
            }

            internal static async Task<(ContractState contractState, IReadOnlyList<(string key, string value)> storagePairs)>
                DownloadContractStateAsync(string contractHash, string rpcUri, uint stateHeight)
            {
                const byte Prefix_Contract = 8;
                const int COR_E_KEYNOTFOUND = unchecked((int)0x80131577);

                if (!UInt160.TryParse(contractHash, out var _contractHash))
                {
                    throw new ArgumentException($"Invalid contract hash: \"{contractHash}\"");
                }

                if (!Program.TryParseRpcUri(rpcUri, out var uri))
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

                ContractState contractState;
                try
                {
                    var proof = await stateAPI.GetProofAsync(stateRoot.RootHash, NativeContract.ContractManagement.Hash, key)
                        .ConfigureAwait(false);
                    var (_, value) = Neo.BlockchainToolkit.Utility.VerifyProof(stateRoot.RootHash, proof);
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

                    var results = (Neo.IO.Json.JArray)response["results"];
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
                    var (provenKey, provenItem) = Neo.BlockchainToolkit.Utility.VerifyProof(rootHash, proofBytes);

                    var key = Convert.FromBase64String(result["key"].AsString());
                    if (!provenKey.Key.Span.SequenceEqual(key)) throw new Exception("Incorrect StorageKey");

                    var value = Convert.FromBase64String(result["value"].AsString());
                    if (!provenItem.AsSpan().SequenceEqual(value)) throw new Exception("Incorrect StorageItem");
                }
            }
        }
    }
}