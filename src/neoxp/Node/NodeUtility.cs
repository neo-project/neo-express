// Copyright (C) 2015-2024 The Neo Project.
//
// NodeUtility.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Json;
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
using System.Numerics;
using static Neo.BlockchainToolkit.Utility;

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
                if (signingContext.Completed)
                    break;
            }
            if (!signingContext.Completed)
                throw new Exception("block signing incomplete");
            block.Header.Witness = signingContext.GetWitnesses()[0];

            return block;
        }

        public static async Task FastForwardAsync(Header prevHeader, uint blockCount, TimeSpan timestampDelta, KeyPair[] keyPairs, uint network, Func<Block, Task> submitBlockAsync)
        {
            if (timestampDelta.TotalSeconds < 0)
                throw new ArgumentException($"Negative {nameof(timestampDelta)} not supported");
            if (blockCount == 0)
                return;

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
            if (oracleNodes.Count == 0)
                throw new Exception("No oracle nodes available. Have you enabled oracles via the `oracle enable` command?");

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
            if (engine.Execute() != Neo.VM.VMState.HALT)
                return null;
            tx.NetworkFee += engine.FeeConsumed;

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

        // constants from ContractManagement native contracts
        const byte Prefix_Contract = 8;
        const byte Prefix_NextAvailableId = 15;

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
                uint? localRootIndex;
                try
                {
                    (localRootIndex, validatedRootIndex) = await stateAPI.GetStateHeightAsync().ConfigureAwait(false);
                }
                catch (RpcException e) when (e.Message.Contains("Method not found"))
                {
                    throw new Exception(
                        "Could not get state information. Make sure the remote RPC server has state service support");
                }

                stateHeight = validatedRootIndex ?? localRootIndex
                    ?? throw new Exception($"GetStateHeight did not return local or validated root index");
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
                var (_, value) = VerifyProof(stateRoot.RootHash, proof);
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

            if (contractState.Id < 0)
                throw new NotSupportedException("Contract download not supported for native contracts");

            var states = Enumerable.Empty<(string key, string value)>();
            ReadOnlyMemory<byte> start = default;

            while (true)
            {
                var @params = StateAPI.MakeFindStatesParams(stateRoot.RootHash, _contractHash, default, start.Span);
                var response = await rpcClient.RpcSendAsync("findstates", @params).ConfigureAwait(false);

                var results = (JArray)response["results"]!;
                if (results.Count == 0)
                    break;

                ValidateProof(stateRoot.RootHash, (JString)response["firstProof"]!, (JObject)results[0]!);

                if (results.Count > 1)
                {
                    ValidateProof(stateRoot.RootHash, (JString)response["lastProof"]!, (JObject)results[^1]!);
                }

                states = states.Concat(results
                    .Select(j => (
                        j!["key"]!.AsString(),
                        j!["value"]!.AsString()
                    )));

                var truncated = response["truncated"]!.AsBoolean();
                if (!truncated)
                    break;
                start = Convert.FromBase64String(results[^1]!["key"]!.AsString());
            }

            return (contractState, states.ToList());

            static void ValidateProof(UInt256 rootHash, JString proof, JObject result)
            {
                var proofBytes = Convert.FromBase64String(proof.AsString());
                var (provenKey, provenItem) = VerifyProof(rootHash, proofBytes);

                var key = Convert.FromBase64String(result["key"]!.AsString());
                if (!provenKey.Key.Span.SequenceEqual(key))
                    throw new Exception("Incorrect StorageKey");

                var value = Convert.FromBase64String(result["value"]!.AsString());
                if (!provenItem.AsSpan().SequenceEqual(value))
                    throw new Exception("Incorrect StorageItem");
            }
        }

        public static int PersistContract(NeoSystem neoSystem, ContractState state,
            IReadOnlyList<(string key, string value)> storagePairs, ContractCommand.OverwriteForce force)
        {
            if (state.Id < 0)
                throw new ArgumentException("PersistContract not supported for native contracts", nameof(state));

            using var snapshot = neoSystem.GetSnapshot();

            StorageKey key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_Contract).Add(state.Hash);
            var localContract = snapshot.GetAndChange(key)?.GetInteroperable<ContractState>();
            if (localContract is null)
            {
                // if localContract is null, the downloaded contract does not exist in the local Express chain
                // Save the downloaded state + storage directly to the local chain

                state.Id = GetNextAvailableId(snapshot);
                snapshot.Add(key, new StorageItem(state));
                PersistStoragePairs(snapshot, state.Id, storagePairs);

                snapshot.Commit();
                return state.Id;
            }

            // if localContract is not null, compare the current state + storage to the downloaded state + storage
            // and overwrite changes if specified by user option

            var (overwriteContract, overwriteStorage) = force switch
            {
                ContractCommand.OverwriteForce.All => (true, true),
                ContractCommand.OverwriteForce.ContractOnly => (true, false),
                ContractCommand.OverwriteForce.None => (false, false),
                ContractCommand.OverwriteForce.StorageOnly => (false, true),
                _ => throw new NotSupportedException($"Invalid OverwriteForce value {force}"),
            };

            var dirty = false;

            if (!ContractStateEquals(state, localContract))
            {
                if (overwriteContract)
                {
                    // Note: a ManagementContract.Update() will not change the contract hash. Not even if the NEF changed.
                    localContract.Nef = state.Nef;
                    localContract.Manifest = state.Manifest;
                    localContract.UpdateCounter = state.UpdateCounter;
                    dirty = true;
                }
                else
                {
                    throw new Exception("Downloaded contract already exists. Use --force to overwrite");
                }
            }

            if (!ContractStorageEquals(localContract.Id, snapshot, storagePairs))
            {
                if (overwriteStorage)
                {
                    byte[] prefix_key = StorageKey.CreateSearchPrefix(localContract.Id, default);
                    foreach (var (k, v) in snapshot.Find(prefix_key))
                    {
                        snapshot.Delete(k);
                    }
                    PersistStoragePairs(snapshot, localContract.Id, storagePairs);
                    dirty = true;
                }
                else
                {
                    throw new Exception("Downloaded contract storage already exists. Use --force to overwrite");
                }
            }

            if (dirty)
                snapshot.Commit();
            return localContract.Id;

            static int GetNextAvailableId(DataCache snapshot)
            {
                StorageKey key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_NextAvailableId);
                StorageItem item = snapshot.GetAndChange(key);
                int value = (int)(BigInteger)item;
                item.Add(1);
                return value;
            }

            static void PersistStoragePairs(DataCache snapshot, int contractId, IReadOnlyList<(string key, string value)> storagePairs)
            {
                for (int i = 0; i < storagePairs.Count; i++)
                {
                    snapshot.Add(
                        new StorageKey { Id = contractId, Key = Convert.FromBase64String(storagePairs[i].key) },
                        new StorageItem(Convert.FromBase64String(storagePairs[i].value)));
                }
            }

            static bool ContractStateEquals(ContractState a, ContractState b)
            {
                return a.Hash.Equals(b.Hash)
                    && a.UpdateCounter == b.UpdateCounter
                    && a.Nef.ToArray().SequenceEqual(b.Nef.ToArray())
                    && a.Manifest.ToJson().ToByteArray(false).SequenceEqual(b.Manifest.ToJson().ToByteArray(false));
            }

            static bool ContractStorageEquals(int contractId, DataCache snapshot, IReadOnlyList<(string key, string value)> storagePairs)
            {
                IReadOnlyDictionary<string, string> storagePairMap = storagePairs.ToDictionary(p => p.key, p => p.value);
                var storageCount = 0;

                byte[] prefixKey = StorageKey.CreateSearchPrefix(contractId, default);
                foreach (var (k, v) in snapshot.Find(prefixKey))
                {
                    var storageKey = Convert.ToBase64String(k.Key.Span);
                    if (storagePairMap.TryGetValue(storageKey, out var storageValue)
                        && storageValue.Equals(Convert.ToBase64String(v.Value.Span)))
                    {
                        storageCount++;
                    }
                    else
                    {
                        return false;
                    }
                }

                return storageCount != storagePairs.Count;
            }
        }

        public static int PersistStorageKeyValuePair(NeoSystem neoSystem, ContractState state,
            (string key, string value) storagePair, ContractCommand.OverwriteForce force)
        {
            if (state.Id < 0)
                throw new ArgumentException("PersistStorage not supported for native contracts", nameof(state));

            using var snapshot = neoSystem.GetSnapshot();

            StorageKey key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_Contract).Add(state.Hash);
            var localContract = snapshot.GetAndChange(key)?.GetInteroperable<ContractState>();
            if (localContract is null)
            {
                // if localContract is null, the contract does not exist in the local Express chain
                throw new Exception("Contract not found");
            }

            var overwriteStorage = force switch
            {
                ContractCommand.OverwriteForce.All => true,
                ContractCommand.OverwriteForce.ContractOnly => false,
                ContractCommand.OverwriteForce.None => false,
                ContractCommand.OverwriteForce.StorageOnly => true,
                _ => throw new NotSupportedException($"Invalid OverwriteForce value {force}"),
            };

            var dirty = false;

            if (overwriteStorage)
            {
                byte[] prefix_key = StorageKey.CreateSearchPrefix(localContract.Id, default);
                foreach (var (k, v) in snapshot.Find(prefix_key))
                {
                    snapshot.Delete(k);
                }

                PersistStoragePair(snapshot, localContract.Id, storagePair);
                dirty = true;
            }
            else
            {
                dirty = PersistStoragePair(snapshot, localContract.Id, storagePair);
            }

            if (dirty)
                snapshot.Commit();
            return localContract.Id;

            static bool PersistStoragePair(DataCache snapshot, int contractId, (string key, string value) storagePair)
            {
                try
                {
                    snapshot.Add(
                        new StorageKey { Id = contractId, Key = Convert.FromBase64String(storagePair.key) },
                        new StorageItem(Convert.FromBase64String(storagePair.value)));

                    return true;
                }
                catch
                {
                    return false;
                }
            }
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
            void ISerializable.Serialize(BinaryWriter writer) => throw new NotImplementedException();
            void IVerifiable.SerializeUnsigned(BinaryWriter writer) => throw new NotImplementedException();
            void ISerializable.Deserialize(ref MemoryReader reader) => throw new NotImplementedException();
            void IVerifiable.DeserializeUnsigned(ref MemoryReader reader) => throw new NotImplementedException();
        }
    }
}
