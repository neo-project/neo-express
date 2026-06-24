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
        /// <summary>Raised when the debugged contract emits a notification while stepping forward.</summary>
        event EventHandler<(UInt160 scriptHash, string scriptName, string eventName, NeoArray state)>? DebugNotify;

        /// <summary>Raised when the debugged contract writes a runtime log while stepping forward.</summary>
        event EventHandler<(UInt160 scriptHash, string scriptName, string message)>? DebugLog;

        /// <summary>Returns whether the current invocation stack contains a catch block.</summary>
        bool CatchBlockOnStack();

        /// <summary>Advances execution to the next VM instruction and updates the current engine state.</summary>
        bool ExecuteNextInstruction();

        /// <summary>Moves execution back to the previous VM instruction when the backend supports it.</summary>
        bool ExecutePrevInstruction();

        /// <summary>Attempts to resolve bytecode for the specified contract script hash.</summary>
        bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script);

        /// <summary>Gets the raw storage key/value pairs available for the specified contract at the current step.</summary>
        IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> GetStorages(UInt160 scriptHash);

        /// <summary>Gets whether this backend can step backward.</summary>
        bool SupportsStepBack { get; }

        /// <summary>Gets the Neo address version associated with the debugged trace or chain.</summary>
        byte AddressVersion { get; }

        /// <summary>Gets the current invocation stack, with the active frame first.</summary>
        IReadOnlyCollection<IExecutionContext> InvocationStack { get; }

        /// <summary>Gets the active invocation frame, or <see langword="null"/> before execution has started.</summary>
        IExecutionContext? CurrentContext { get; }

        /// <summary>Gets the result stack after execution has halted.</summary>
        IReadOnlyList<StackItem> ResultStack { get; }

        /// <summary>Gets the gas consumed at the current step.</summary>
        long GasConsumed { get; }

        /// <summary>Gets the fault captured from the debugged execution, if any.</summary>
        Exception? FaultException { get; }

        /// <summary>Gets the VM state at the current step.</summary>
        VMState State { get; }

        /// <summary>Gets whether the backend cursor is positioned before the first VM instruction.</summary>
        bool AtStart { get; }
    }
}
