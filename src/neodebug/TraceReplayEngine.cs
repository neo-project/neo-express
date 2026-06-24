// Copyright (C) 2015-2026 The Neo Project.
//
// TraceReplayEngine.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.SmartContract;
using Neo.VM;
using System.Diagnostics.CodeAnalysis;
using NeoArray = Neo.VM.Types.Array;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// An <see cref="IApplicationEngine"/> backed by a recorded <c>.neo-trace</c>. It steps a
    /// <see cref="TraceDebugReader"/> cursor forwards and backwards, projecting each recorded
    /// <see cref="TraceRecord"/> into the engine's current state — so this engine, unlike the live one,
    /// supports stepping backward (time-travel debugging).
    /// </summary>
    /// <remarks>
    /// Named to distinguish it from bctklib's <c>TraceApplicationEngine</c>, which <em>produces</em> traces;
    /// this one <em>replays</em> them.
    /// </remarks>
    internal sealed partial class TraceReplayEngine : IApplicationEngine
    {
        private readonly TraceDebugReader _reader;
        private readonly Dictionary<UInt160, Script> _seedContracts;
        private TraceRecord? _currentTraceRecord;
        private IReadOnlyList<TraceRecord.StackFrame> _stackFrames = Array.Empty<TraceRecord.StackFrame>();
        private bool _disposed;

        public TraceReplayEngine(TraceDebugReader reader, IEnumerable<KeyValuePair<UInt160, Script>>? seedContracts = null)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _seedContracts = seedContracts is null ? new() : new(seedContracts);

            // Advance to the first recorded VM step so the engine opens positioned at the entry point.
            while (_reader.TryGetNext(out var record))
            {
                ProcessRecord(record);
                if (record is TraceRecord trace)
                {
                    _currentTraceRecord = trace;
                    break;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _reader.Dispose();
        }

        public bool SupportsStepBack => true;

        public bool ExecuteNextInstruction()
        {
            while (_reader.TryGetNext(out var record))
            {
                ProcessRecord(record);
                if (record is TraceRecord trace && !ReferenceEquals(trace, _currentTraceRecord))
                {
                    _currentTraceRecord = trace;
                    return true;
                }
            }

            return false;
        }

        public bool ExecutePrevInstruction()
        {
            while (_reader.TryGetPrev(out var record))
            {
                ProcessRecord(record, stepBack: true);
                if (record is TraceRecord trace && !ReferenceEquals(trace, _currentTraceRecord))
                {
                    _currentTraceRecord = trace;
                    return true;
                }
            }

            return false;
        }

        private void ProcessRecord(ITraceDebugRecord record, bool stepBack = false)
        {
            switch (record)
            {
                case TraceRecord trace:
                    State = trace.State;
                    GasConsumed = trace.GasConsumed;
                    _stackFrames = trace.StackFrames;
                    InvocationStack = trace.StackFrames.Select(CreateContext).ToList();
                    break;
                case StorageRecord:
                    break;
                case NotifyRecord notify:
                    if (!stepBack)
                        DebugNotify?.Invoke(this, (notify.ScriptHash, notify.ScriptName, notify.EventName, new NeoArray(notify.State)));
                    break;
                case LogRecord log:
                    if (!stepBack)
                        DebugLog?.Invoke(this, (log.ScriptHash, log.ScriptName, log.Message));
                    break;
                case ResultsRecord results:
                    ResultStack = stepBack ? Array.Empty<StackItem>() : results.ResultStack;
                    break;
                case FaultRecord fault:
                    FaultException = stepBack ? null : new Exception(fault.Exception);
                    break;
                default:
                    throw new InvalidDataException($"Unexpected trace record {record.GetType().Name}");
            }
        }

        private ExecutionContextAdapter CreateContext(TraceRecord.StackFrame frame)
            => new(frame, ResolveScript(frame));

        private Script ResolveScript(TraceRecord.StackFrame frame)
        {
            if (_seedContracts.TryGetValue(frame.ScriptIdentifier, out var script))
                return script;
            if (_reader.TryGetContract(frame.ScriptIdentifier, out script))
                return script;

            // A trace records only the entry script; any other frame's bytecode (a native contract, or a
            // deployed contract not supplied to the debugger) is unavailable here. Represent it with an empty
            // script so the frame still appears on the call stack rather than failing the whole replay.
            return EmptyScript;
        }

        private static readonly Script EmptyScript = new(ReadOnlyMemory<byte>.Empty);

        public bool AtStart => _reader.AtStart;
        public byte AddressVersion => _reader.AddressVersion;
        public VMState State { get; private set; }
        public IReadOnlyCollection<IExecutionContext> InvocationStack { get; private set; } = Array.Empty<IExecutionContext>();
        public IExecutionContext? CurrentContext => InvocationStack.FirstOrDefault();
        public IReadOnlyList<StackItem> ResultStack { get; private set; } = Array.Empty<StackItem>();
        public Exception? FaultException { get; private set; }
        public long GasConsumed { get; private set; }

        public event EventHandler<(UInt160 scriptHash, string scriptName, string eventName, NeoArray state)>? DebugNotify;
        public event EventHandler<(UInt160 scriptHash, string scriptName, string message)>? DebugLog;

        public bool CatchBlockOnStack() => _stackFrames.Any(f => f.HasCatch);

        public bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script)
        {
            if (_seedContracts.TryGetValue(scriptHash, out script))
                return true;
            return _reader.TryGetContract(scriptHash, out script);
        }

        public IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> GetStorages(UInt160 scriptHash)
            => _reader.FindStorage(scriptHash);
    }
}
