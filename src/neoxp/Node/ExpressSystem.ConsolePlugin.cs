using System;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;

namespace NeoExpress.Node
{
    partial class ExpressSystem
    {
        class ConsolePlugin : Plugin, ILogPlugin
        {
            NeoSystem? neoSystem = null;
            readonly IConsole console;

            public ConsolePlugin(IConsole console)
            {
                this.console = console;
                ApplicationEngine.Log += OnLog!;
            }

            public void WriteLine(string text) => console.WriteLine(text);

            protected override void OnSystemLoaded(NeoSystem system)
            {
                if (neoSystem is not null)
                {
                    neoSystem = system;
                    _ = neoSystem.ActorSystem.ActorOf(EventWrapper<Blockchain.ApplicationExecuted>.Props(OnApplicationExecuted));
                }

                base.OnSystemLoaded(system);
            }

            void OnLog(object sender, LogEventArgs args)
            {
                var engine = sender as ApplicationEngine;
                var tx = engine?.ScriptContainer as Transaction;
                var colorCode = tx?.Witnesses?.Any() ?? false ? "96" : "93";

                var contract = neoSystem is not null
                    ? NativeContract.ContractManagement.GetContract(neoSystem.StoreView, args.ScriptHash)
                    : null;
                var name = contract is not null ? contract.Manifest.Name : args.ScriptHash.ToString();
                Console.WriteLine($"\x1b[35m{name}\x1b[0m Log: \x1b[{colorCode}m\"{args.Message}\"\x1b[0m [{args.ScriptContainer.GetType().Name}]");
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
                    console.Error.WriteLine($"\x1b[31m{logMessage}\x1b[0m");
                }
            }

            void ILogPlugin.Log(string source, LogLevel level, object message)
            {
                console.Out.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.ff} {source} {level} {message}");
            }
        }
    }
}
