using McMaster.Extensions.CommandLineUtils;
using Neo.Consensus;
using Neo.Network.P2P;
using Neo.Plugins;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Neo.Express.Commands
{
    [Command("run")]
    class RunCommand
    {
        [Option]
        [Required]
        int NodeIndex { get; }

        [Option]
        string Input { get; }

        [Option]
        uint SecondsPerBlock { get; }

        class LogPlugin : Plugin, ILogPlugin
        {
            readonly IConsole console;

            public LogPlugin(IConsole console)
            {
                this.console = console;
            }

            public override void Configure()
            {
            }

            void ILogPlugin.Log(string source, LogLevel level, string message)
            {
                console.WriteLine($"{DateTimeOffset.Now.ToString("HH:mm:ss.ff")} {source} {level} {message}");
            }
        }

        int OnExecute(CommandLineApplication app, IConsole console)
        {
            var input = string.IsNullOrEmpty(Input)
                ? Path.Combine(Directory.GetCurrentDirectory(), "express.privatenet.json")
                : Input;

            if (!File.Exists(input))
            {
                console.WriteLine($"{input} doesn't exist");
                app.ShowHelp();
                return 1;
            }

            DevChain LoadChain()
            {
                using (var stream = File.OpenRead(input))
                {
                    var doc = JsonDocument.Parse(stream);
                    if (!DevChain.InitializeProtocolSettings(doc, SecondsPerBlock))
                    {
                        throw new Exception("Couldn't initialize protocol settings");
                    }
                    return DevChain.Parse(doc);
                }
            }

            var chain = LoadChain();
            if (NodeIndex >= chain.ConensusNodes.Count || NodeIndex < 0)
            {
                console.WriteLine("Invalid node index");
                app.ShowHelp();
                return 1;
            }

            var conensusNode = chain.ConensusNodes[NodeIndex];
            var cts = new CancellationTokenSource();
            var basePort = (NodeIndex + 1) * 10000;

            const string ROOT_PATH = @"C:\Users\harry\neoexpress";
            var path = Path.Combine(ROOT_PATH, conensusNode.GetAccounts().Single(a => a.IsDefault).Address);

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            Directory.CreateDirectory(path);

            Task.Factory.StartNew(() =>
            {
                using (var store = new Persistence.LevelDB.LevelDBStore(path))
                using (var system = new NeoSystem(store))
                {
                    var logPlugin = new LogPlugin(console);

                    system.StartNode(new ChannelsConfig()
                    {
                        Tcp = new IPEndPoint(IPAddress.Any, basePort + 1),
                        WebSocket = new IPEndPoint(IPAddress.Any, basePort + 2)
                    });
                    system.StartConsensus(conensusNode);
                    system.StartRpc(IPAddress.Any, basePort + 2, conensusNode);

                    cts.Token.WaitHandle.WaitOne();
                }
            });

            console.CancelKeyPress += (sender, args) => cts.Cancel();

            cts.Token.WaitHandle.WaitOne();
            return 0;
        }
    }
}