using System;
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
        // In next preview, Execute method will be virtual so it will be easier to
        // retrieve start/end state. In the meantime, preExecTrace and completeStateChange
        // are used to control tracing of start/end states.

        private bool preExecTrace = true;
        private readonly ITraceDebugSink traceDebugSink;

        public ExpressApplicationEngine(ITraceDebugSink traceDebugSink, TriggerType trigger, IVerifiable container, StoreView snapshot, long gas)
            : base(trigger, container, snapshot, gas)
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
            if (ReferenceEquals(sender, this))
            {
                traceDebugSink.Notify(args);
            }
        }

        private void OnLog(object sender, LogEventArgs args)
        {
            if (ReferenceEquals(sender, this))
            {
                traceDebugSink.Log(args);
            }
        }

        protected override void PreExecuteInstruction()
        {
            if (preExecTrace)
            {
                traceDebugSink.Script(CurrentContext.Script);
                traceDebugSink.Trace(State, InvocationStack);
                WriteStorages(CurrentScriptHash);

                preExecTrace = false;
            }

            base.PreExecuteInstruction();
        }

        protected override void PostExecuteInstruction()
        {
            base.PostExecuteInstruction();

            if (State == VMState.HALT)
            {
                traceDebugSink.Results(State, GasConsumed, ResultStack);
            }
            traceDebugSink.Trace(State, InvocationStack);
            WriteStorages(CurrentScriptHash);
        }

        protected override void OnFault(Exception e)
        {
            base.OnFault(e);
            traceDebugSink.Fault(e);
            traceDebugSink.Trace(State, InvocationStack);
        }

        private void WriteStorages(UInt160 scriptHash)
        {
            if (scriptHash != null)
            {
                var contractState = Snapshot.Contracts.TryGet(scriptHash);
                if (contractState != null)
                {
                    var storages = Snapshot.Storages.Find(CreateSearchPrefix(contractState.Id, default));
                    traceDebugSink.Storages(scriptHash, storages);
                }
            }

            // TODO: remove this copy of CreateSearchPrefix in preview 4 
            //       https://github.com/neo-project/neo/pull/1824
            static byte[] CreateSearchPrefix(int id, ReadOnlySpan<byte> prefix)
            {
                byte[] buffer = new byte[sizeof(int) + prefix.Length];
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buffer, id);
                prefix.CopyTo(buffer.AsSpan(sizeof(int)));
                return buffer;
            }
        }
    }
}
