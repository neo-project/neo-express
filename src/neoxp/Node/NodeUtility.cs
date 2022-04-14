using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
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

namespace NeoExpress.Node
{
    class NodeUtility
    {
        public static Block CreateSignedBlock(Header prevHeader, IReadOnlyList<KeyPair> keyPairs, uint network, Transaction[]? transactions = null, ulong timestamp = 0)
        {
            transactions ??= Array.Empty<Transaction>();

            var blockHeight = prevHeader.Index + 1;
            var block = new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = prevHeader.Hash,
                    MerkleRoot = MerkleTree.ComputeRoot(transactions.Select(t => t.Hash).ToArray()),
                    Timestamp = timestamp > prevHeader.Timestamp
                        ? timestamp
                        : Math.Max(Neo.Helper.ToTimestampMS(DateTime.UtcNow), prevHeader.Timestamp + 1),
                    Index = blockHeight,
                    PrimaryIndex = 0,
                    NextConsensus = prevHeader.NextConsensus
                },
                Transactions = transactions
            };

            // generate the block header witness. Logic lifted from ConsensusContext.CreateBlock
            var m = keyPairs.Count - (keyPairs.Count - 1) / 3;
            var contract = Contract.CreateMultiSigContract(m, keyPairs.Select(k => k.PublicKey).ToList());
            var signingContext = new ContractParametersContext(null, new BlockScriptHashes(prevHeader.NextConsensus), network);
            for (int i = 0; i < keyPairs.Count; i++)
            {
                var signature = block.Header.Sign(keyPairs[i], network);
                signingContext.AddSignature(contract, keyPairs[i].PublicKey, signature);
                if (signingContext.Completed) break;
            }
            if (!signingContext.Completed) throw new Exception("block signing incomplete");
            block.Header.Witness = signingContext.GetWitnesses()[0];

            return block;
        }

        public static async Task FastForwardAsync(Header prevHeader, uint blockCount, TimeSpan timestampDelta, KeyPair[] keyPairs, uint network, Func<Block, Task> submitBlockAsync)
        {
            if (timestampDelta.TotalSeconds < 0) throw new ArgumentException($"Negative {nameof(timestampDelta)} not supported");
            if (blockCount == 0) return;

            var timestamp = Math.Max(Neo.Helper.ToTimestampMS(DateTime.UtcNow), prevHeader.Timestamp + 1);
            var delta = (ulong)timestampDelta.TotalMilliseconds;

            if (blockCount == 1)
            {
                var block = CreateSignedBlock(
                    prevHeader, keyPairs, network, timestamp: timestamp + delta);
                await submitBlockAsync(block).ConfigureAwait(false);
            }
            else
            {
                var period = delta / (blockCount - 1);
                for (int i = 0; i < blockCount; i++)
                {
                    var block = CreateSignedBlock(
                        prevHeader, keyPairs, network, timestamp: timestamp);
                    await submitBlockAsync(block).ConfigureAwait(false);
                    prevHeader = block.Header;
                    timestamp += period;
                }
            }
        }

        public static void SignOracleResponseTransaction(ProtocolSettings settings, ExpressChain chain, Transaction tx, IReadOnlyList<ECPoint> oracleNodes)
        {
            var signatures = new Dictionary<ECPoint, byte[]>();

            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var account = chain.ConsensusNodes[i].Wallet.DefaultAccount ?? throw new Exception("Invalid DefaultAccount");
                var key = DevWalletAccount.FromExpressWalletAccount(settings, account).GetKey() ?? throw new Exception("Invalid KeyPair");
                if (oracleNodes.Contains(key.PublicKey))
                {
                    signatures.Add(key.PublicKey, tx.Sign(key, chain.Network));
                }
            }

            int m = oracleNodes.Count - (oracleNodes.Count - 1) / 3;
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

        // Copied from OracleService.CreateResponseTx to avoid taking dependency on OracleService package and it's 110mb GRPC runtime
        public static Transaction? CreateResponseTx(DataCache snapshot, OracleRequest request, OracleResponse response, IReadOnlyList<ECPoint> oracleNodes, ProtocolSettings settings)
        {
            if (oracleNodes.Count == 0) throw new Exception("No oracle nodes available. Have you enabled oracles via the `oracle enable` command?");

            var requestTx = NativeContract.Ledger.GetTransactionState(snapshot, request.OriginalTxid);
            var n = oracleNodes.Count;
            var m = n - (n - 1) / 3;
            var oracleSignContract = Contract.CreateMultiSigContract(m, oracleNodes);

            var tx = new Transaction()
            {
                Version = 0,
                Nonce = unchecked((uint)response.Id),
                ValidUntilBlock = requestTx.BlockIndex + settings.MaxValidUntilBlockIncrement,
                Signers = new[]
                {
                    new Signer
                    {
                        Account = NativeContract.Oracle.Hash,
                        Scopes = WitnessScope.None
                    },
                    new Signer
                    {
                        Account = oracleSignContract.ScriptHash,
                        Scopes = WitnessScope.None
                    }
                },
                Attributes = new[] { response },
                Script = OracleResponse.FixedScript,
                Witnesses = new Witness[2]
            };
            Dictionary<UInt160, Witness> witnessDict = new Dictionary<UInt160, Witness>
            {
                [oracleSignContract.ScriptHash] = new Witness
                {
                    InvocationScript = Array.Empty<byte>(),
                    VerificationScript = oracleSignContract.Script,
                },
                [NativeContract.Oracle.Hash] = new Witness
                {
                    InvocationScript = Array.Empty<byte>(),
                    VerificationScript = Array.Empty<byte>(),
                }
            };

            UInt160[] hashes = tx.GetScriptHashesForVerifying(snapshot);
            tx.Witnesses[0] = witnessDict[hashes[0]];
            tx.Witnesses[1] = witnessDict[hashes[1]];

            // Calculate network fee

            var oracleContract = NativeContract.ContractManagement.GetContract(snapshot, NativeContract.Oracle.Hash);
            var engine = ApplicationEngine.Create(TriggerType.Verification, tx, snapshot.CreateSnapshot(), settings: settings);
            ContractMethodDescriptor md = oracleContract.Manifest.Abi.GetMethod("verify", -1);
            engine.LoadContract(oracleContract, md, CallFlags.None);
            if (engine.Execute() != Neo.VM.VMState.HALT) return null;
            tx.NetworkFee += engine.GasConsumed;

            var executionFactor = NativeContract.Policy.GetExecFeeFactor(snapshot);
            var networkFee = executionFactor * Neo.SmartContract.Helper.MultiSignatureContractCost(m, n);
            tx.NetworkFee += networkFee;

            // Base size for transaction: includes const_header + signers + script + hashes + witnesses, except attributes

            int size_inv = 66 * m;
            int size = Transaction.HeaderSize + tx.Signers.GetVarSize() + tx.Script.GetVarSize()
                + Neo.IO.Helper.GetVarSize(hashes.Length) + witnessDict[NativeContract.Oracle.Hash].Size
                + Neo.IO.Helper.GetVarSize(size_inv) + size_inv + oracleSignContract.Script.GetVarSize();

            var feePerByte = NativeContract.Policy.GetFeePerByte(snapshot);
            if (response.Result.Length > OracleResponse.MaxResultSize)
            {
                response.Code = OracleResponseCode.ResponseTooLarge;
                response.Result = Array.Empty<byte>();
            }
            else if (tx.NetworkFee + (size + tx.Attributes.GetVarSize()) * feePerByte > request.GasForResponse)
            {
                response.Code = OracleResponseCode.InsufficientFunds;
                response.Result = Array.Empty<byte>();
            }
            size += tx.Attributes.GetVarSize();
            tx.NetworkFee += size * feePerByte;

            // Calculate system fee

            tx.SystemFee = request.GasForResponse - tx.NetworkFee;

            return tx;
        }

        public const byte Prefix_Contract = 8;
        private const byte Prefix_NextAvailableId = 15;

        private static int LastUsedContractId(DataCache snapshot)
        {
            StorageKey key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_NextAvailableId);
            StorageItem item = snapshot.TryGet(key);
            return (int)(BigInteger)item - 1;
        }

        private static void SetNextAvailableContractId(DataCache snapshot, int newId)
        {
            StorageKey key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_NextAvailableId);
            StorageItem item = snapshot.GetAndChange(key);
            item.Set(newId);
        }
        
        private static int GetNextAvailableId(DataCache snapshot)
        {
            StorageKey key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_NextAvailableId);
            StorageItem item = snapshot.GetAndChange(key);
            int value = (int)(BigInteger)item;
            item.Add(1);
            return value;
        }

        public static async Task<(ContractState contractState, IReadOnlyList<(string key, string value)> storagePairs)> DownloadContractStateAsync(
            string contractHash,
            string rpcUri,
            uint stateHeight)
        {
            if (!UInt160.TryParse(contractHash, out var _contractHash))
            {
                throw new ArgumentException($"Invalid contract hash: \"{contractHash}\"");    
            }

            if (!TransactionExecutor.TryParseRpcUri(rpcUri, out var uri))
            {
                throw new ArgumentException($"Invalid RpcUri value \"{rpcUri}\"");
            }

            using var rpcClient = new RpcClient(uri);
            var stateAPI = new StateAPI(rpcClient);
                
            uint height = stateHeight;
            if (height == 0)
            {
                uint? localRootIndex;
                try
                {
                    (localRootIndex, _) = await stateAPI.GetStateHeightAsync();
                }
                catch (RpcException e)
                {
                    if (e.Message.Contains("Method not found"))
                    {
                        throw new Exception(
                            "Could not get state information. Make sure the remote RPC server has state service support");
                    }
                    throw;
                }

                height = localRootIndex.HasValue ? localRootIndex.Value
                    : throw new Exception($"Null \"{nameof(localRootIndex)}\" in state height response");
            }

            var stateRoot = await stateAPI.GetStateRootAsync(height);

            // rpcClient.GetContractStateAsync returns the current ContractState, but this method needs
            // the ContractState as it was at stateHeight. ContractManagement stores ContractState by
            // contractHash with the prefix 8. The following code uses stateAPI.GetStateAsync to retrieve
            // the value with that key at the height state root and then deserializes it into a ContractState
            // instance via GetInteroperable.

            var key = new byte[21];
            key[0] = 8; // ContractManagement.Prefix_Contract
            _contractHash.ToArray().CopyTo(key, 1);

            var contractStateBuffer = await stateAPI.GetStateAsync(
                stateRoot.RootHash, NativeContract.ContractManagement.Hash, key);
            var contractState = new StorageItem(contractStateBuffer).GetInteroperable<ContractState>();
            var states = await rpcClient.ExpressFindStatesAsync(stateRoot.RootHash, _contractHash, new byte[0]);

            return (contractState, states);
        }

        public static int PersistContract(SnapshotCache snapshot, ContractState state,
            IReadOnlyList<(string key, string value)> storagePairs, ContractCommand.OverwriteForce force)
        {
            StorageKey key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_Contract).Add(state.Hash);
            var localContract = snapshot.GetAndChange(key)?.GetInteroperable<ContractState>();

            if (localContract is null)
            {
                // Our local chain might already be using the contract id of the pulled contract, we need to check for this
                // to avoid having contracts with duplicate id's. This is important because the contract id is part of the
                // StorageContext used with Storage syscalls and else we'll potentially override storage keys or iterate
                // over keys that shouldn't exist for one of the contracts.
                if (state.Id <= LastUsedContractId(snapshot))
                {
                    state.Id = GetNextAvailableId(snapshot);
                }
                else
                {
                    // Update available id such that a regular contract deploy will use the right next id;
                    SetNextAvailableContractId(snapshot, state.Id + 1);
                }

                snapshot.Add(key, new StorageItem(state));
            }
            else
            {
                if (force == ContractCommand.OverwriteForce.None)
                {
                    List<(string key, string value)> states = new();
                    byte[] prefixKey = StorageKey.CreateSearchPrefix(localContract.Id, new byte[]{});
                    foreach (var (k, v) in snapshot.Find(prefixKey))
                    {
                        states.Add((Convert.ToBase64String(k.Key.ToArray()), Convert.ToBase64String(v.ToArray())));
                    }
                    
                    var stateEquals = localContract.ToJson().ToByteArray(false)
                        .SequenceEqual(state.ToJson().ToByteArray(false));
                    var storageEquals = storagePairs.SequenceEqual(states);
                    
                    if (stateEquals && storageEquals)
                    {
                        throw new Exception("Contract already exists - aborting");
                    } 
                    if (stateEquals && !storageEquals)
                    {
                        throw new Exception("Contract already exists - storage differs.\nUse --force:StorageOnly to overwrite");    
                    }
                    if (!stateEquals && storageEquals)
                    {
                        throw new Exception("Contract already exists - contract state differs.\nUse --force:ContractOnly to overwrite");    
                    }
                    if (!stateEquals && !storageEquals)
                    {
                        throw new Exception("Contract already exists - contract state and storage differs.\nUse --force:All to overwrite");
                    }
                }

                if (force == ContractCommand.OverwriteForce.All ||
                    force == ContractCommand.OverwriteForce.ContractOnly)
                {
                    // Note: a ManagementContract.Update() will not change the contract hash. Not even if the NEF changed.
                    localContract.Nef = state.Nef;
                    localContract.Manifest = state.Manifest;
                    localContract.UpdateCounter = state.UpdateCounter;
                }
            }

            if ((force == ContractCommand.OverwriteForce.All || force == ContractCommand.OverwriteForce.StorageOnly) ||
                (localContract is null && force == ContractCommand.OverwriteForce.None))
            {
                var contractId = localContract is null ? state.Id : localContract.Id;

                // the storage schema might have changed therefore we first clear all storage
                byte[] prefix_key = StorageKey.CreateSearchPrefix(contractId, new byte[] { });
                foreach (var (k, v) in snapshot.Find(prefix_key))
                {
                    snapshot.Delete(k);
                }

                for (int i = 0; i < storagePairs.Count; i++)
                {
                    snapshot.Add(
                        new StorageKey { Id = contractId, Key = Convert.FromBase64String(storagePairs[i].key) },
                        new StorageItem(Convert.FromBase64String(storagePairs[i].value)));
                }
            }

            snapshot.Commit();
            return state.Id;
        }

        // Need an IVerifiable.GetScriptHashesForVerifying implementation that doesn't
        // depend on the DataCache snapshot parameter in order to create a 
        // ContractParametersContext without direct access to node data.

        class BlockScriptHashes : IVerifiable
        {
            readonly UInt160[] hashes;

            public BlockScriptHashes(UInt160 scriptHash)
            {
                hashes = new[] { scriptHash };
            }

            public UInt160[] GetScriptHashesForVerifying(DataCache snapshot) => hashes;

            Witness[] IVerifiable.Witnesses
            {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }

            int ISerializable.Size => throw new NotImplementedException();
            void ISerializable.Deserialize(BinaryReader reader) => throw new NotImplementedException();
            void IVerifiable.DeserializeUnsigned(BinaryReader reader) => throw new NotImplementedException();
            void ISerializable.Serialize(BinaryWriter writer) => throw new NotImplementedException();
            void IVerifiable.SerializeUnsigned(BinaryWriter writer) => throw new NotImplementedException();

        }
    }
}
