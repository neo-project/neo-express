// Copyright (C) 2015-2025 The Neo Project.
//
// WorkNetLogPlugin.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System.Collections.Generic;
using System.Diagnostics;
using static Crayon.Output;

namespace NeoWorkNet.Commands;

class WorkNetLogPlugin : Plugin
{
    readonly object engineLock = new();
    readonly List<WeakReference<ApplicationEngine>> engineRefs = new();
    NeoSystem? neoSystem;
    readonly IConsole console;

    public WorkNetLogPlugin(IConsole console, Action<string, object?>? diagnosticWriter = null)
    {
        if (diagnosticWriter is not null)
        {
            var stateServiceObserver = new KeyValuePairObserver(diagnosticWriter);
            var diagnosticObserver = new DiagnosticObserver(StateServiceStore.LoggerCategory, stateServiceObserver);
            DiagnosticListener.AllListeners.Subscribe(diagnosticObserver);
        }

        this.console = console;
        Blockchain.Committing += OnCommitting;
        ApplicationEngine.InstanceHandler += OnApplicationEngineCreated;
        Neo.Utility.Logging += OnNeoUtilityLog;
    }

    public override void Dispose()
    {
        Neo.Utility.Logging -= OnNeoUtilityLog;
        ApplicationEngine.InstanceHandler -= OnApplicationEngineCreated;
        lock (engineLock)
        {
            foreach (var engineRef in engineRefs)
            {
                if (engineRef.TryGetTarget(out var engine))
                {
                    engine.Log -= OnAppEngineLog;
                }
            }
            engineRefs.Clear();
        }
        Blockchain.Committing -= OnCommitting;
        GC.SuppressFinalize(this);
    }

    void OnApplicationEngineCreated(ApplicationEngine engine)
    {
        lock (engineLock)
        {
            engineRefs.Add(new WeakReference<ApplicationEngine>(engine));
        }
        engine.Log += OnAppEngineLog;
    }

    protected override void OnSystemLoaded(NeoSystem system)
    {
        if (neoSystem is not null)
            throw new Exception($"{nameof(OnSystemLoaded)} already called");
        neoSystem = system;
        base.OnSystemLoaded(system);
    }

    protected string GetContractName(UInt160 scriptHash)
    {
        if (neoSystem is not null)
        {
            var contract = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, scriptHash);
            if (contract is not null)
            {
                return contract.Manifest.Name;
            }
        }

        return scriptHash.ToString();
    }

    void OnAppEngineLog(ApplicationEngine engine, LogEventArgs args)
    {
        var container = args.ScriptContainer is null
            ? string.Empty
            : $" [{args.ScriptContainer.GetType().Name}]";


        console.WriteLine($"{Magenta(GetContractName(args.ScriptHash))} Log: \"{Cyan(args.Message)}\" {container}");
    }

    void OnNeoUtilityLog(string source, LogLevel level, object message)
    {
        console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.ff} {source} {level} {message}");
    }

    void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
    {
        foreach (var appExec in applicationExecutedList)
        {
            OnApplicationExecuted(appExec);
        }
    }

    void OnApplicationExecuted(Blockchain.ApplicationExecuted applicationExecuted)
    {
        if (applicationExecuted.VMState == Neo.VM.VMState.FAULT)
        {
            var logMessage = $"Tx FAULT: hash={applicationExecuted.Transaction.Hash}";
            if (!string.IsNullOrEmpty(applicationExecuted.Exception.Message))
            {
                logMessage += $" exception=\"{applicationExecuted.Exception.Message}\"";
            }
            console.Error.WriteLine(Red(logMessage));
        }
    }
}
