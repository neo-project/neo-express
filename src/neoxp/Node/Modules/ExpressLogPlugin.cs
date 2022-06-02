using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.IO;

namespace NeoExpress.Node
{
    class ExpressLogPlugin : Plugin
    {
        NeoSystem? neoSystem;
        readonly IConsole console;

        public ExpressLogPlugin(IConsole console)
        {
            this.console = console;

            Blockchain.Committing += OnCommitting;
            ApplicationEngine.Log += OnAppEngineLog!;
            Neo.Utility.Logging += OnNeoUtilityLog;
        }

        public override void Dispose()
        {
            Neo.Utility.Logging -= OnNeoUtilityLog;
            ApplicationEngine.Log -= OnAppEngineLog!;
            Blockchain.Committing -= OnCommitting;
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (this.neoSystem is not null) throw new Exception($"{nameof(OnSystemLoaded)} already called");
            neoSystem = system;
            base.OnSystemLoaded(system);
        }

        string GetContractName(UInt160 scriptHash)
        {
            if (neoSystem != null)
            {
                var contract = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, scriptHash);
                if (contract != null)
                {
                    return contract.Manifest.Name;
                }
            }

            return scriptHash.ToString();
        }

        void OnAppEngineLog(object sender, LogEventArgs args)
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
