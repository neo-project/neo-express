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
        private NeoSystem? neoSystem;
        private readonly TextWriter writer;

        public LogPlugin(TextWriter writer)
        {
            this.writer = writer;
            ApplicationEngine.Log += OnLog!;
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
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

        void OnLog(object sender, LogEventArgs args)
        {
            var container = args.ScriptContainer is null
                ? string.Empty
                : $" [{args.ScriptContainer.GetType().Name}]";
            writer.WriteLine($"\x1b[35m{GetContractName(args.ScriptHash)}\x1b[0m Log: \x1b[96m\"{args.Message}\"\x1b[0m{container}");
        }

        void ILogPlugin.Log(string source, LogLevel level, object message)
        {
            writer.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.ff} {source} {level} {message}");
        }
    }
}
