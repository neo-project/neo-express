using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;

namespace NeoExpress.Node
{
    class NodeUtility
    {
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
                var block = NodeUtility.CreateSignedBlock(
                                prevHeader,
                                keyPairs,
                                network,
                                timestamp: timestamp + delta);
                await submitBlockAsync(block).ConfigureAwait(false);
            }
            else
            {
                var period = delta / (blockCount - 1);
                for (int i = 0; i < blockCount; i++)
                {
                    var block = NodeUtility.CreateSignedBlock(
                                    prevHeader,
                                    keyPairs,
                                    network,
                                    timestamp: timestamp);
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
    }
}
