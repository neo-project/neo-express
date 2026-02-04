// Copyright (C) 2015-2026 The Neo Project.
//
// TraceApplicationEngine.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.TraceDebug;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Collections.Immutable;

namespace Neo.BlockchainToolkit.SmartContract
{
    public class TraceApplicationEngine : ApplicationEngine
    {
        readonly ITraceDebugSink traceDebugSink;
        ImmutableDictionary<UInt160, string> contractNameMap = ImmutableDictionary<UInt160, string>.Empty;

        public TraceApplicationEngine(ITraceDebugSink traceDebugSink, TriggerType trigger, IVerifiable container,
                                      DataCache snapshot, Block? persistingBlock, ProtocolSettings settings, long gas,
                                      IDiagnostic? diagnostic = null, JumpTable? jumpTable = null)
            : base(trigger, container, snapshot, persistingBlock, settings, gas, diagnostic, jumpTable)
        {
            this.traceDebugSink = traceDebugSink;

            Log += OnLog;
            Notify += OnNotify;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Log -= OnLog;
                Notify -= OnNotify;
                traceDebugSink.Dispose();
            }
            base.Dispose(disposing);
        }

        private string GetContractName(UInt160 scriptId)
        {
            return ImmutableInterlocked.GetOrAdd(ref contractNameMap, scriptId,
                k => NativeContract.ContractManagement.GetContract(SnapshotCache, k)?.Manifest.Name ?? string.Empty);
        }

        private void OnNotify(ApplicationEngine engine, NotifyEventArgs args)
        {
            traceDebugSink.Notify(args, GetContractName(args.ScriptHash));
        }

        private void OnLog(ApplicationEngine engine, LogEventArgs args)
        {
            traceDebugSink.Log(args, GetContractName(args.ScriptHash));
        }

        public override VMState Execute()
        {
            traceDebugSink.ProtocolSettings(ProtocolSettings.Network, ProtocolSettings.AddressVersion);
            traceDebugSink.Script(CurrentContext?.Script ?? Array.Empty<byte>());
            traceDebugSink.Trace(State, FeeConsumed, InvocationStack);
            WriteStorages(CurrentScriptHash);

            return base.Execute();
        }

        protected override void PostExecuteInstruction(Instruction instruction)
        {
            base.PostExecuteInstruction(instruction);

            if (State == VMState.HALT)
            {
                traceDebugSink.Results(State, FeeConsumed, ResultStack);
            }
            traceDebugSink.Trace(State, FeeConsumed, InvocationStack);
            WriteStorages(CurrentScriptHash);
        }

        protected override void OnFault(Exception e)
        {
            base.OnFault(e);
            traceDebugSink.Fault(e);
            traceDebugSink.Trace(State, FeeConsumed, InvocationStack);
        }

        private void WriteStorages(UInt160 scriptHash)
        {
            if (scriptHash != null)
            {
                var contractState = NativeContract.ContractManagement.GetContract(SnapshotCache, scriptHash);
                if (contractState != null)
                {
                    var storages = SnapshotCache.Find(StorageKey.CreateSearchPrefix(contractState.Id, default));
                    traceDebugSink.Storages(scriptHash, storages);
                }
            }
        }
    }
}
