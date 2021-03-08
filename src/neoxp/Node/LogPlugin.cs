using Neo;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.IO;

namespace NeoExpress.Node
{
    class LogPlugin : Plugin, ILogPlugin
    {
        private readonly NeoSystem neoSystem;
        private readonly TextWriter writer;

        public LogPlugin(NeoSystem neoSystem, TextWriter writer)
        {
            this.neoSystem = neoSystem;
            this.writer = writer;
            ApplicationEngine.Log += OnLog!;
        }

        private void OnLog(object sender, LogEventArgs args)
        {
            var contract = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, args.ScriptHash);
            var name = contract == null ? args.ScriptHash.ToString() : contract.Manifest.Name;
            writer.WriteLine($"\x1b[35m{name}\x1b[0m Log: \x1b[96m\"{args.Message}\"\x1b[0m [{args.ScriptContainer.GetType().Name}]");
        }

        void ILogPlugin.Log(string source, LogLevel level, object message)
        {
            writer.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.ff} {source} {level} {message}");
        }
    }
}
