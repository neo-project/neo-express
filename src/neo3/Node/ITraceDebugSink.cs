using System;
using System.Collections.Generic;
using Neo;
using Neo.Ledger;
using Neo.SmartContract;
using Neo.VM;

namespace NeoExpress.Neo3.Node
{
    public interface ITraceDebugSink : IDisposable
    {
        void Trace(VMState vmState, IReadOnlyCollection<ExecutionContext> executionContexts);
        void Log(LogEventArgs args, string scriptName);
        void Notify(NotifyEventArgs args, string scriptName);
        void Results(VMState vmState, long gasConsumed, IReadOnlyCollection<Neo.VM.Types.StackItem> results);
        void Fault(Exception exception);
        void Script(byte[] script);
        void Storages(UInt160 scriptHash, IEnumerable<(StorageKey key, StorageItem item)> storages);
    }
}
