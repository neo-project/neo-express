using McMaster.Extensions.CommandLineUtils;
using Neo.Plugins;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Neo.Express.Commands
{
    [Command("run")]
    internal class RunCommand
    {
        [Argument(0)]
        private int? NodeIndex { get; }

        [Option]
        private string Input { get; }

        [Option]
        private uint SecondsPerBlock { get; }

        [Option]
        private bool Reset { get; }

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var backend = Program.GetBackend();
                var cts = backend.RunBlockchain(Input, Program.ROOT_PATH, NodeIndex, SecondsPerBlock, Reset, s => console.WriteLine(s));
                console.CancelKeyPress += (sender, args) => cts.Cancel();
                cts.Token.WaitHandle.WaitOne();
                return 0;
            }
            catch (Exception ex)
            {
                console.WriteError(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
    }
}