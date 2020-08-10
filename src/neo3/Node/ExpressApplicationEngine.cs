using System;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;

namespace NeoExpress.Neo3.Node
{
    // random notes:
    //  * Need to have a trace record for capturing the initial execution script.
    //    I think we can skip capturing any script that is deployed in the chain.
    //    If the developer has the script / source locally, we can step thru it.
    //    If they don't, then we can simply skip to the end of the missing script's 
    //    execution.
    //  * Instead of capturing storages every time, I'm thinking we capture storages
    //    on context entry and only capture them again if storage put/putex/delete
    //    is called. I can watch for those methods to be called in OnSysCall w/o
    //    having to do the full parameter parsing stuff.
    //  * In next preview, Execute method will be virtual so it will be easier to
    //    retrieve start/end state 

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

            // // TODO: PR Opened to make StorageKey.CreateSearchPrefix public
            // //       If accepted, remove this copy of that method 
            // //       https://github.com/neo-project/neo/pull/1824
            // static byte[] CreateSearchPrefix(int id, ReadOnlySpan<byte> prefix)
            // {
            //     byte[] buffer = new byte[sizeof(int) + prefix.Length];
            //     BinaryPrimitives.WriteInt32LittleEndian(buffer, id);
            //     prefix.CopyTo(buffer.AsSpan(sizeof(int)));
            //     return buffer;
            // }
        // }

         protected override void PreExecuteInstruction()
         {
             // Note, PR opened to add overridable OnExecutionStarted method.
             //       If accepted, move this logic to there
             //       https://github.com/neo-project/neo-vm/pull/355
             if (preExecTrace)
             {
                 traceDebugSink.Script(CurrentContext.Script);
                 traceDebugSink.Trace(State, InvocationStack);
                 preExecTrace = false;
             }

             base.PreExecuteInstruction();
         }

        protected override void PostExecuteInstruction(Instruction instruction)
        {
            traceDebugSink.Trace(State, InvocationStack);
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
