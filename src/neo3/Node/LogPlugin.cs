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
            var name = args.ScriptHash.ToString();
            writer.WriteLine($"{name} Log \"{args.Message}\" [{args.ScriptContainer.GetType().Name}]");
        }

        void ILogPlugin.Log(string source, LogLevel level, object message)
        {
            writer.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.ff} {source} {level} {message}");
        }
    }
}
