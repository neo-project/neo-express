using Neo.Plugins;
using System;
using System.IO;

namespace NeoExpress.Neo2.Node
{
    internal class LogPlugin : Plugin, ILogPlugin
    {
        private readonly TextWriter writer;

        public LogPlugin(TextWriter writer)
        {
            this.writer = writer;
        }

        public override void Configure()
        {
        }

        void ILogPlugin.Log(string source, LogLevel level, string message)
        {
            writer.WriteLine($"{DateTimeOffset.Now.ToString("HH:mm:ss.ff")} {source} {level} {message}");
        }
    }
}
