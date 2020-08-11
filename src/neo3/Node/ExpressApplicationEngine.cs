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
    // random notes:
    //  * Instead of capturing storages every time, I'm thinking we capture storages
    //    on context entry and only capture them again if storage put/putex/delete
    //    is called. I can watch for those methods to be called in OnSysCall w/o
    //    having to do the full parameter parsing stuff. This way, I don't capture
    //    storage every time + I'm only capturing current script context changes
    //  * I think I need need to trace Throw opcodes special


    internal class ExpressApplicationEngine : ApplicationEngine
    {
        // In next preview, Execute method will be virtual so it will be easier to
        // retrieve start/end state. In the meantime, preExecTrace and completeStateChange
        // are used to control tracing of start/end states.

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

        protected override void PostExecuteInstruction(Instruction instruction)
        {
            base.PostExecuteInstruction(instruction);

            traceDebugSink.Trace(State, InvocationStack);
            WriteStorages(CurrentScriptHash);
            // TODO: move this results call to Execute override in preview 4
            if (State == VMState.HALT)
            {
                traceDebugSink.Results(State, GasConsumed, ResultStack);
            }
        }

        protected override void OnFault(Exception e)
        {
            traceDebugSink.Fault(e);
            // TODO: move this results call to Execute override in preview 4
            traceDebugSink.Results(State, GasConsumed, ResultStack);
            base.OnFault(e);
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

// private void Trace()
// {
// var storages = InvocationStack
//     .Select(ec => ec.GetScriptHash())
//     .Distinct()
//     .SelectMany(GetStorages);

// IEnumerable<(UInt160 scriptHash, byte[] key, StorageItem item)> GetStorages(UInt160 scriptHash)
// {
//     var contractState = Snapshot.Contracts.TryGet(scriptHash);
//     return contractState != null
//         ? Snapshot.Storages
//             .Find(CreateSearchPrefix(contractState.Id, default))
//             .Select(t => (scriptHash, t.Key.Key, t.Value))
//         : Enumerable.Empty<(UInt160, byte[], StorageItem)>();
// }


// }
