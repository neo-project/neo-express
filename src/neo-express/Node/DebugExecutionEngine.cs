using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;

using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using NeoDebug;
using NeoDebug.VariableContainers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoExpress.Node
{
    internal class DebugExecutionEngine : ApplicationEngine, IExecutionEngine
    {
        private readonly UInt160 scriptHash;
        private readonly Neo.Persistence.Snapshot snapshot;

        public DebugExecutionEngine(UInt160 scriptHash, TriggerType trigger, IScriptContainer container, Neo.Persistence.Snapshot snapshot, Neo.Fixed8 gas, bool testMode = false) 
            : base(trigger, container, snapshot, gas, testMode)
        {
            this.scriptHash = scriptHash;
            this.snapshot = snapshot;
        }

        public static IExecutionEngine CreateExecutionEngine(NeoDebug.Models.Contract contract, LaunchArguments arguments)
        {
            UInt160 ParseAddress(string address) =>
                UInt160.TryParse(address, out var result) ? result : address.ToScriptHash();

            (IEnumerable<CoinReference> inputs, IEnumerable<TransactionOutput> outputs) GetUtxo()
            {
                if (arguments.ConfigurationProperties.TryGetValue("utxo", out var utxo))
                {
                    var _inputs = utxo["inputs"]?.Select(t => new CoinReference()
                        {
                            PrevHash = UInt256.Parse(t.Value<string>("txid")),
                            PrevIndex = t.Value<ushort>("value")
                        });

                    var _outputs = utxo["outputs"]?.Select(t => new TransactionOutput()
                        {
                            AssetId = NodeUtility.GetAssetId(t.Value<string>("asset")),
                            Value = Fixed8.FromDecimal(t.Value<decimal>("value")),
                            ScriptHash = ParseAddress(t.Value<string>("address"))
                        });

                    return (_inputs, _outputs);
                }

                return (null, null);
            }

            var scriptHash = new UInt160(contract.ScriptHash);

            var snapshot = Blockchain.Singleton.GetSnapshot();
            snapshot = snapshot.Contracts.TryGet(scriptHash) == null
                    ? new DebugSnapshot(contract, Blockchain.Singleton.GetSnapshot())
                    : snapshot;

            var (inputs, outputs) = GetUtxo();

            var tx = new InvocationTransaction
            {
                Version = 1,
                Script = contract.Script,
                Attributes = Array.Empty<TransactionAttribute>(),
                Inputs = inputs?.ToArray() ?? Array.Empty<CoinReference>(),
                Outputs = outputs?.ToArray() ?? Array.Empty<TransactionOutput>(),
                Witnesses = Array.Empty<Witness>(),
            };

            return new DebugExecutionEngine(
                scriptHash,
                TriggerType.Application,
                tx,
                snapshot,
                default,
                true);
        }

        VMState IExecutionEngine.State { get => State; set { State = value; } }

        IEnumerable<StackItem> IExecutionEngine.ResultStack => ResultStack;

        ExecutionContext IExecutionEngine.CurrentContext => CurrentContext;

        RandomAccessStack<ExecutionContext> IExecutionEngine.InvocationStack => InvocationStack;

        void IExecutionEngine.ExecuteNext() => ExecuteNext();

        IVariableContainer IExecutionEngine.GetStorageContainer(IVariableContainerSession session)
            => new StorageServiceContainer(session, snapshot, scriptHash);

        ExecutionContext IExecutionEngine.LoadScript(byte[] script, int rvcount) => LoadScript(script, rvcount);
    }
}
