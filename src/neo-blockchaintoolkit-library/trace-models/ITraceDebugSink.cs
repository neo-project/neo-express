// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.SmartContract;
using Neo.VM;
using System;
using System.Collections.Generic;

namespace Neo.BlockchainToolkit.TraceDebug
{
    public interface ITraceDebugSink : IDisposable
    {
        void Trace(VMState vmState, long gasConsumed, IReadOnlyCollection<ExecutionContext> executionContexts);
        void Log(LogEventArgs args, string scriptName);
        void Notify(NotifyEventArgs args, string scriptName);
        void Results(VMState vmState, long gasConsumed, IReadOnlyCollection<Neo.VM.Types.StackItem> results);
        void Fault(Exception exception);
        void Script(Script script);
        void Storages(UInt160 scriptHash, IEnumerable<(StorageKey key, StorageItem item)> storages);
        void ProtocolSettings(uint network, byte addressVersion);
    }
}
