// Copyright (C) 2015-2026 The Neo Project.
//
// DebugApplicationEngine.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Diagnostics.CodeAnalysis;
using NeoArray = Neo.VM.Types.Array;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// An <see cref="IApplicationEngine"/> that debugs a contract by stepping a live, in-process VM (a
    /// <see cref="TestApplicationEngine"/>) one instruction at a time. Unlike the trace-replay engine it cannot
    /// step backward, so <see cref="SupportsStepBack"/> is <see langword="false"/>.
    /// </summary>
    internal partial class DebugApplicationEngine : TestApplicationEngine, IApplicationEngine
    {
        private readonly DataCache _snapshot;
        private readonly InvocationStackAdapter _invocationStackAdapter;
        private readonly IDictionary<UInt160, UInt160> _scriptIdMap = new Dictionary<UInt160, UInt160>();

        public event EventHandler<(UInt160 scriptHash, string scriptName, string eventName, NeoArray state)>? DebugNotify;
        public event EventHandler<(UInt160 scriptHash, string scriptName, string message)>? DebugLog;

        public DebugApplicationEngine(IVerifiable? container, DataCache snapshot, ProtocolSettings settings, Block? persistingBlock, Func<byte[], bool>? witnessChecker)
            : base(TriggerType.Application, container, snapshot, persistingBlock, settings, ApplicationEngine.TestModeGas, witnessChecker)
        {
            _snapshot = snapshot;
            Log += OnLog;
            Notify += OnNotify;
            _invocationStackAdapter = new InvocationStackAdapter(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Log -= OnLog;
                Notify -= OnNotify;
                (_snapshot as IDisposable)?.Dispose();
            }
            base.Dispose(disposing);
        }

        bool IApplicationEngine.SupportsStepBack => false;

        private void OnNotify(ApplicationEngine sender, NotifyEventArgs args)
            => DebugNotify?.Invoke(sender, (args.ScriptHash, GetContractName(args.ScriptHash), args.EventName, args.State));

        private void OnLog(ApplicationEngine sender, LogEventArgs args)
            => DebugLog?.Invoke(sender, (args.ScriptHash, GetContractName(args.ScriptHash), args.Message));

        private string GetContractName(UInt160 scriptHash)
            => NativeContract.ContractManagement.GetContract(SnapshotCache, scriptHash)?.Manifest?.Name ?? string.Empty;

        public bool ExecuteNextInstruction()
        {
            AtStart = false;
            ExecuteNext();
            return true;
        }

        public bool ExecutePrevInstruction()
            => throw new NotSupportedException("The live engine cannot step backward; use trace replay for time-travel.");

        public bool CatchBlockOnStack()
        {
            foreach (var executionContext in InvocationStack)
            {
                if (executionContext.TryStack?.Any(c => c.HasCatch) == true)
                    return true;
            }

            return false;
        }

        public bool TryGetContract(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script)
        {
            var contractState = NativeContract.ContractManagement.GetContract(SnapshotCache, scriptHash);
            if (contractState != null)
            {
                script = contractState.Script;
                return true;
            }

            script = default;
            return false;
        }

        public IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> GetStorages(UInt160 scriptHash)
        {
            var contractId = NativeContract.ContractManagement.GetContract(SnapshotCache, scriptHash)?.Id;
            return contractId.HasValue
                ? SnapshotCache.Find(StorageKey.CreateSearchPrefix(contractId.Value, default), SeekDirection.Forward)
                    .Select(t => ((ReadOnlyMemory<byte>)t.Key.Key, t.Value))
                : Enumerable.Empty<(ReadOnlyMemory<byte>, StorageItem)>();
        }

        IReadOnlyCollection<IExecutionContext> IApplicationEngine.InvocationStack => _invocationStackAdapter;

        IReadOnlyList<StackItem> IApplicationEngine.ResultStack => ResultStack;

        IExecutionContext? IApplicationEngine.CurrentContext => CurrentContext == null
            ? null
            : new ExecutionContextAdapter(CurrentContext, _scriptIdMap);

        public bool AtStart { get; private set; } = true;

        public byte AddressVersion => ProtocolSettings.AddressVersion;
    }
}
