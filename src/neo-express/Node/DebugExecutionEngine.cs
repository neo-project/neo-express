using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.Cryptography.ECC;
using Neo.IO.Caching;
using Neo.IO.Wrappers;
using Neo.Ledger;
using Neo.SmartContract;
using Neo.VM;
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

        public static IExecutionEngine CreateExecutionEngine(NeoDebug.Models.Contract contract, LaunchArguments __)
        {
            var scriptHash = new UInt160(contract.ScriptHash);

            var snapshot = Blockchain.Singleton.GetSnapshot();
            snapshot = snapshot.Contracts.TryGet(scriptHash) == null
                    ? new DebugSnapshot(contract, Blockchain.Singleton.GetSnapshot())
                    : snapshot;

            return new DebugExecutionEngine(
                scriptHash,
                TriggerType.Application,
                null,
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
