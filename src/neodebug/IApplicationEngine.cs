// Copyright (C) 2015-2026 The Neo Project.
//
// IApplicationEngine.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.SmartContract;
using Neo.VM;
using System.Diagnostics.CodeAnalysis;
using NeoArray = Neo.VM.Types.Array;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// The backend-agnostic execution engine a debug session drives. Two implementations exist: a
    /// trace-replay engine (which steps a recorded <c>.neo-trace</c> forwards and backwards) and a live
    /// engine (which steps a running VM). The session never depends on which one it is talking to.
    /// </summary>
    /// <remarks>
    /// Storage is exposed as the raw key/value pairs of a contract (<see cref="GetStorages"/>) rather than as a
    /// presentation-layer container, so this interface carries no Debug Adapter Protocol dependency; rendering
    /// storage as debug variables is the session/variable layer's concern.
    /// </remarks>
    internal interface IApplicationEngine : IDisposable
    {
        event EventHandler<(UInt160 scriptHash, string scriptName, string eventName, NeoArray state)>? DebugNotify;
        event EventHandler<(UInt160 scriptHash, string scriptName, string message)>? DebugLog;

        bool CatchBlockOnStack();
        bool ExecuteNextInstruction();
        bool ExecutePrevInstruction();
        bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script);
        IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> GetStorages(UInt160 scriptHash);

        bool SupportsStepBack { get; }
        byte AddressVersion { get; }
        IReadOnlyCollection<IExecutionContext> InvocationStack { get; }
        IExecutionContext? CurrentContext { get; }
        IReadOnlyList<StackItem> ResultStack { get; }
        long GasConsumed { get; }
        Exception? FaultException { get; }
        VMState State { get; }
        bool AtStart { get; }
    }
}
