using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using Neo;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;

namespace NeoExpress.Neo3.Node
{
    internal class ExpressApplicationEngine : ApplicationEngine
    {
        private bool preExecTrace = true;
        private readonly ITraceDebugSink traceDebugSink;

        public ExpressApplicationEngine(ITraceDebugSink traceDebugSink, TriggerType trigger, IVerifiable container, StoreView snapshot, long gas, bool testMode) 
            : base(trigger, container, snapshot, gas, testMode)
        {
            this.traceDebugSink = traceDebugSink;
            Log += OnLog;
            Notify += OnNotify;
        }

        public override void Dispose()
        {
            Log -= OnLog;
            Notify -= OnNotify;
            traceDebugSink.Dispose();
            base.Dispose();
        }

        private void OnNotify(object sender, NotifyEventArgs args)
        {
            if (object.ReferenceEquals(sender, this))
            {
                traceDebugSink.Notify(args);
            }
        }

        private void OnLog(object sender, LogEventArgs args)
        {
            if (object.ReferenceEquals(sender, this))
            {
                traceDebugSink.Log(args);
            }
        }

        private void Trace()
        {
            var storages = InvocationStack
                .Select(ec => ec.GetScriptHash())
                .Distinct()
                .SelectMany(GetStorages);

            traceDebugSink.Trace(State, InvocationStack, storages);

            IEnumerable<(UInt160 scriptHash, byte[] key, StorageItem item)> GetStorages(UInt160 scriptHash)
            {
                var contractState = Snapshot.Contracts.TryGet(scriptHash);
                return contractState != null
                    ? Snapshot.Storages
                        .Find(CreateSearchPrefix(contractState.Id, default))
                        .Select(t => (scriptHash, t.Key.Key, t.Value))
                    : Enumerable.Empty<(UInt160, byte[], StorageItem)>();
            }

            // TODO: PR Opened to make StorageKey.CreateSearchPrefix public
            //       If accepted, remove this copy of that method 
            //       https://github.com/neo-project/neo/pull/1824
            static byte[] CreateSearchPrefix(int id, ReadOnlySpan<byte> prefix)
            {
                byte[] buffer = new byte[sizeof(int) + prefix.Length];
                BinaryPrimitives.WriteInt32LittleEndian(buffer, id);
                prefix.CopyTo(buffer.AsSpan(sizeof(int)));
                return buffer;
            }
        }

         protected override void PreExecuteInstruction()
         {
             // Note, PR opened to add overridable OnExecutionStarted method.
             //       If accepted, move this logic to there
             //       https://github.com/neo-project/neo-vm/pull/355
             if (preExecTrace)
             {
                 Trace();
                 preExecTrace = false;
             }

             base.PreExecuteInstruction();
         }

        protected override void PostExecuteInstruction(Instruction instruction)
        {
            Trace();
            base.PostExecuteInstruction(instruction);
        }

        protected override void OnStateChanged()
        {
            // Proposed Logic:
            //  * Ignore state change to None
            //  * capture Result Stack / gas consumed / etc on state change to Halt
            //  * override OnFault to capture fault info 
            if (State == VMState.HALT)
            {
                traceDebugSink.Results(State, GasConsumed, ResultStack);
            }
            base.OnStateChanged();
        }

        protected override void OnFault(Exception e)
        {
            traceDebugSink.Fault(e);
            base.OnFault(e);
        }
    }
}
