using Neo;
using Neo.Plugins;
using Neo.SmartContract;
using System;
using System.IO;

namespace NeoExpress.Neo3.Node
{
    class LogPlugin : Plugin, ILogPlugin
    {
        private readonly TextWriter writer;

        public LogPlugin(TextWriter writer)
        {
            this.writer = writer;
            ApplicationEngine.Log += OnLog;
        }

        private void OnLog(object sender, LogEventArgs args)
        {
            var contract = Neo.Ledger.Blockchain.Singleton.View.Contracts.TryGet(args.ScriptHash);
            var name = contract == null ? args.ScriptHash.ToString() : contract.Manifest.Name;
            writer.WriteLine($"\x1b[35m{name}\x1b[0m Log: \x1b[96m\"{args.Message}\"\x1b[0m [{args.ScriptContainer.GetType().Name}]");
        }

        void ILogPlugin.Log(string source, LogLevel level, object message)
        {
            writer.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.ff} {source} {level} {message}");
        }
    }
}
