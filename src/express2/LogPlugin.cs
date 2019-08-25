using Neo.Plugins;
using System;

namespace NeoExpress.Neo2Backend
{
    public partial class Neo2Backend
    {
        private class LogPlugin : Plugin, ILogPlugin
        {
            private readonly Action<string> writeConsole;

            public LogPlugin(Action<string> writeConsole)
            {
                this.writeConsole = writeConsole;
            }

            public override void Configure()
            {
            }

            void ILogPlugin.Log(string source, LogLevel level, string message)
            {
                Console.WriteLine($"{DateTimeOffset.Now.ToString("HH:mm:ss.ff")} {source} {level} {message}");
            }
        }
    }
}
