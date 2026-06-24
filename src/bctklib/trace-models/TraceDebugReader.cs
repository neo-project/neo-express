// Copyright (C) 2015-2026 The Neo Project.
//
// TraceDebugReader.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using MessagePack;
using MessagePack.Resolvers;
using Neo.SmartContract;
using Neo.VM;
using System.Diagnostics.CodeAnalysis;

namespace Neo.BlockchainToolkit.TraceDebug
{
    /// <summary>
    /// Reads a <c>.neo-trace</c> stream of MessagePack <see cref="ITraceDebugRecord"/> records (as written
    /// by <see cref="TraceDebugStream"/> / the <c>neotrace</c> tool) and exposes a forward/backward cursor
    /// over them. This is the replay counterpart to the trace writer: a debugger steps the cursor to move
    /// through a recorded execution in either direction.
    /// </summary>
    /// <remarks>
    /// <see cref="ScriptRecord"/> and <see cref="ProtocolSettingsRecord"/> are side records consumed silently
    /// as the stream advances — their contents surface through <see cref="TryGetContract"/>,
    /// <see cref="Network"/>, and <see cref="AddressVersion"/>. Every other record kind (trace, notify, log,
    /// results, fault, storage) is returned from <see cref="TryGetNext"/>. Backward movement is served from a
    /// history of already-read records, so the underlying stream is only ever read forward once.
    /// </remarks>
    public sealed class TraceDebugReader : IDisposable
    {
        private static readonly MessagePackSerializerOptions s_options = MessagePackSerializerOptions.Standard
            .WithResolver(TraceDebugResolver.Instance);

        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private readonly Stack<ITraceDebugRecord> _previous = new();
        private readonly Stack<ITraceDebugRecord> _next = new();
        private readonly Dictionary<UInt160, Script> _contracts = new();
        private bool _disposed;

        /// <summary>The network magic recorded in the trace, or 0 until a protocol-settings record is read.</summary>
        public uint Network { get; private set; }

        /// <summary>The address version recorded in the trace, or 0 until a protocol-settings record is read.</summary>
        public byte AddressVersion { get; private set; }

        /// <summary>Opens a trace file for reading.</summary>
        /// <param name="traceFilePath">Path to a <c>.neo-trace</c> file.</param>
        /// <param name="knownContracts">Scripts to seed the contract table with (for example, from NEF files) so
        /// <see cref="TryGetContract"/> resolves them even before their script record is read.</param>
        public TraceDebugReader(string traceFilePath, IEnumerable<KeyValuePair<UInt160, Script>>? knownContracts = null)
            : this(File.OpenRead(traceFilePath), leaveOpen: false, knownContracts)
        {
        }

        /// <summary>Reads a trace from an existing stream.</summary>
        /// <param name="stream">A stream positioned at the start of the trace records.</param>
        /// <param name="leaveOpen"><see langword="true"/> to leave <paramref name="stream"/> open when this reader is disposed.</param>
        /// <param name="knownContracts">Scripts to seed the contract table with.</param>
        public TraceDebugReader(Stream stream, bool leaveOpen = false, IEnumerable<KeyValuePair<UInt160, Script>>? knownContracts = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _leaveOpen = leaveOpen;
            if (knownContracts is not null)
            {
                foreach (var (hash, script) in knownContracts)
                    _contracts[hash] = script;
            }
        }

        /// <summary><see langword="true"/> when the cursor is before the first returned record.</summary>
        public bool AtStart => _previous.Count == 0;

        /// <summary>Advances the cursor to the next record, skipping (but absorbing) script/protocol-settings side records.</summary>
        public bool TryGetNext([MaybeNullWhen(false)] out ITraceDebugRecord record)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_next.TryPop(out record))
            {
                _previous.Push(record);
                return true;
            }

            while (_stream.Position < _stream.Length)
            {
                record = MessagePackSerializer.Deserialize<ITraceDebugRecord>(_stream, s_options);
                switch (record)
                {
                    case ScriptRecord script:
                        _contracts.TryAdd(script.ScriptHash, script.Script);
                        break;
                    case ProtocolSettingsRecord protocol:
                        Network = protocol.Network;
                        AddressVersion = protocol.AddressVersion;
                        break;
                    default:
                        _previous.Push(record);
                        return true;
                }
            }

            record = null;
            return false;
        }

        /// <summary>Moves the cursor back to the previously returned record.</summary>
        public bool TryGetPrev([MaybeNullWhen(false)] out ITraceDebugRecord record)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_previous.TryPop(out record))
            {
                _next.Push(record);
                return true;
            }

            record = null;
            return false;
        }

        /// <summary>Resolves a contract script by hash from the script records seen so far (and any seeded contracts).</summary>
        public bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script)
            => _contracts.TryGetValue(scriptHash, out script);

        /// <summary>
        /// Returns the storage entries of <paramref name="scriptHash"/> as of the cursor's current position, taken
        /// from the most recent storage record at or before it.
        /// </summary>
        public IEnumerable<(ReadOnlyMemory<byte> key, StorageItem value)> FindStorage(UInt160 scriptHash)
        {
            foreach (var rec in _previous)
            {
                if (rec is StorageRecord storage && storage.ScriptHash.Equals(scriptHash))
                    return storage.Storages.Select(kvp => ((ReadOnlyMemory<byte>)kvp.Key, kvp.Value));
            }

            return Enumerable.Empty<(ReadOnlyMemory<byte>, StorageItem)>();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (!_leaveOpen)
                _stream.Dispose();
        }
    }
}
