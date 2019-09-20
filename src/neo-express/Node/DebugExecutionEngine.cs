using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.SmartContract;
using Neo.VM;
using NeoDebug;
using NeoDebug.VariableContainers;
using System.Collections.Generic;

namespace NeoExpress.Node
{
    internal class DebugExecutionEngine : ApplicationEngine, IExecutionEngine
    {
        private Neo.UInt160 scriptHash;
        private readonly Neo.Persistence.Snapshot snapshot;

        public DebugExecutionEngine(TriggerType trigger, IScriptContainer container, Neo.Persistence.Snapshot snapshot, Neo.Fixed8 gas, bool testMode = false) 
            : base(trigger, container, snapshot, gas, testMode)
        {
            this.snapshot = snapshot;
        }

        public static IExecutionEngine CreateExecutionEngine(NeoDebug.Models.Contract _, LaunchArguments __)
        {
            using (var snapshot = Neo.Ledger.Blockchain.Singleton.GetSnapshot())
            {
                return new DebugExecutionEngine(TriggerType.Application, null, snapshot, default, true);
            }
        }

        VMState IExecutionEngine.State { get => State; set { State = value; } }

        IEnumerable<StackItem> IExecutionEngine.ResultStack => ResultStack;

        ExecutionContext IExecutionEngine.CurrentContext => CurrentContext;

        RandomAccessStack<ExecutionContext> IExecutionEngine.InvocationStack => InvocationStack;

        void IExecutionEngine.ExecuteNext() => ExecuteNext();

        IVariableContainer IExecutionEngine.GetStorageContainer(IVariableContainerSession session)
            => new StorageServiceContainer(session, snapshot, scriptHash);

        void IExecutionEngine.LoadContract(NeoDebug.Models.Contract contract)
        {
            scriptHash = new Neo.UInt160(contract.ScriptHash);
        }

        ExecutionContext IExecutionEngine.LoadScript(byte[] script, int rvcount) => LoadScript(script, rvcount);
    }
}
