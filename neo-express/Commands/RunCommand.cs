using McMaster.Extensions.CommandLineUtils;
using Neo.Plugins;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Express.Commands
{
    [Command("run")]
    internal class RunCommand
    {
        [Argument(0)]
        private int NodeIndex { get; }

        [Option]
        private string Input { get; }

        [Option]
        private uint SecondsPerBlock { get; }

        private class LogPlugin : Plugin, ILogPlugin
        {
            private readonly IConsole console;

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

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            var input = Program.DefaultPrivatenetFileName(Input);
            if (!File.Exists(input))
            {
                console.WriteLine($"{input} doesn't exist");
                app.ShowHelp();
                return 1;
            }

            DevChain LoadChain()
            {
                using (var stream = File.OpenRead(input))
                using (var reader = new JsonTextReader(new StreamReader(stream)))
                {
                    var json = JObject.Load(reader);

                    if (!DevChain.InitializeProtocolSettings(json, SecondsPerBlock))
                    {
                        throw new Exception("Couldn't initialize protocol settings");
                    }

                    return DevChain.FromJson(json);
                }
            }

            var chain = LoadChain();
            if (NodeIndex >= chain.ConsensusNodes.Count || NodeIndex < 0)
            {
                console.WriteLine("Invalid node index");
                app.ShowHelp();
                return 1;
            }

            var consensusNode = chain.ConsensusNodes[NodeIndex];
            var cts = new CancellationTokenSource();

            const string ROOT_PATH = @"C:\Users\harry\neoexpress";
            var path = Path.Combine(ROOT_PATH, consensusNode.Wallet.GetAccounts().Single(a => a.IsDefault).Address);

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            Directory.CreateDirectory(path);

            Task.Factory.StartNew(() =>
            {
                try
                {
                    using (var store = new Persistence.LevelDB.LevelDBStore(path))
                    using (var system = new NeoSystem(store))
                    {
                        var logPlugin = new LogPlugin(console);
                        var rpcPlugin = new ExpressNodeRpcPlugin();

                        system.StartNode(consensusNode.TcpPort, consensusNode.WebSocketPort);
                        system.StartConsensus(consensusNode.Wallet);
                        system.StartRpc(IPAddress.Any, consensusNode.RpcPort, consensusNode.Wallet);

                        cts.Token.WaitHandle.WaitOne();
                    }
                }
                catch (Exception ex)
                {
                    console.WriteLine(ex);
                    cts.Cancel();
                }
            });

            console.CancelKeyPress += (sender, args) => cts.Cancel();

            cts.Token.WaitHandle.WaitOne();
            return 0;
        }
    }
}