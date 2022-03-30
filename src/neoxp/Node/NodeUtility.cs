using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit.Models;
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
            return (int)(BigInteger)item;
        }

        private static void SetLastUsedContractId(DataCache snapshot, int newId)
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

        public static async Task<(ContractState contractState, (string key, string value)[] storagePairs)> DownloadParamsAsync(
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
                var stateHeight_ = await stateAPI.GetStateHeightAsync();
                if (stateHeight_.localRootIndex is null)
                {
                    throw new Exception("Null \"localRootIndex\" in state height response");
                }
                height = stateHeight_.localRootIndex.Value;
            }

            var stateRoot = await stateAPI.GetStateRootAsync(height);
            var states = await rpcClient.ExpressFindStatesAsync(stateRoot.RootHash, _contractHash, new byte[0]);
            var contractState = await rpcClient.GetContractStateAsync(contractHash).ConfigureAwait(false);

            return (contractState, states.Results);
        }
            
        
        public static int PersistContract(SnapshotCache snapshot, ContractState state, (string key, string value)[] storagePairs, ContractCommand.OverwriteForce force)
        {
            StorageKey key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_Contract).Add(state.Hash);
            var localContract = snapshot.GetAndChange(key)?.GetInteroperable<ContractState>();
            
            if (localContract is null)
            {
                // Our local chain might already be using the contract id of the pulled contract, we need to check for this
                // to avoid having contracts with duplicate id's. This is important because the contract id is part of the
                // StorageContext used with Storage syscalls and else we'll potentially override storage keys or iterate
                // over keys that shouldn't exist for one of the contracts.
                if (state.Id < LastUsedContractId(snapshot))
                {
                    state.Id = GetNextAvailableId(snapshot);
                }
                else
                {
                    // Update available id such that a regular contract deploy will use the right next id;
                    SetLastUsedContractId(snapshot, state.Id);
                }
                snapshot.Add(key, new StorageItem(state));
            }
            else 
            {
                if (force == ContractCommand.OverwriteForce.None)
                {
                    throw new Exception("Contract already exists locally. Use --force:<option> to overwrite");
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

            if (force == ContractCommand.OverwriteForce.All || force == ContractCommand.OverwriteForce.StorageOnly)
            {
                var contractId = localContract is null ? state.Id : localContract.Id;

                // the storage schema might have changed therefore we first clear all storage
                byte[] prefix_key = StorageKey.CreateSearchPrefix(contractId, new byte[]{});
                foreach (var (k, v) in snapshot.Find(prefix_key))
                {
                    snapshot.Delete(k);
                }

                foreach (var pair in storagePairs)
                {
                    snapshot.Add(
                        new StorageKey { Id = contractId, Key = Convert.FromBase64String(pair.key) },
                        new StorageItem(Convert.FromBase64String(pair.value))
                    );
                }
            }
            snapshot.Commit();
            return state.Id;
        }
    }
}
