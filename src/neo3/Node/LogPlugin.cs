using Neo;
using Neo.Plugins;
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
        }

        void ILogPlugin.Log(string source, LogLevel level, object message)
        {
            writer.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.ff} {source} {level} {message}");
        }
    }
}
