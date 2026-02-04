// Copyright (C) 2015-2025 The Neo Project.
//
// ExpressLogPlugin.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System.Collections.Generic;

namespace NeoExpress.Node
{
    class ExpressLogPlugin : Plugin
    {
        readonly object engineLock = new();
        readonly List<WeakReference<ApplicationEngine>> engineRefs = new();
        NeoSystem? neoSystem;
        readonly IConsole console;

        public ExpressLogPlugin(IConsole console)
        {
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
            if (this.neoSystem is not null)
                throw new Exception($"{nameof(OnSystemLoaded)} already called");
            neoSystem = system;
            base.OnSystemLoaded(system);
        }

        string GetContractName(UInt160 scriptHash)
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
            console.WriteLine($"\x1b[35m{GetContractName(args.ScriptHash)}\x1b[0m Log: \x1b[96m\"{args.Message}\"\x1b[0m{container}");
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

        void OnApplicationExecuted(Neo.Ledger.Blockchain.ApplicationExecuted applicationExecuted)
        {
            if (applicationExecuted.VMState == Neo.VM.VMState.FAULT)
            {
                var logMessage = $"Tx FAULT: hash={applicationExecuted.Transaction.Hash}";
                if (!string.IsNullOrEmpty(applicationExecuted.Exception.Message))
                {
                    logMessage += $" exception=\"{applicationExecuted.Exception.Message}\"";
                }
                console.Error.WriteLine($"\x1b[31m{logMessage}\x1b[0m");
            }
        }
    }
}
