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

        [Option]
        private bool Reset { get; }

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
            try
            {
                var (chainJson, _) = DevChain.LoadJson(Input);
                if (!DevChain.InitializeProtocolSettings(chainJson, SecondsPerBlock))
                {
                    throw new Exception("Couldn't initialize protocol settings");
                }

                var chain = DevChain.FromJson(chainJson);
                if (NodeIndex >= chain.ConsensusNodes.Count || NodeIndex < 0)
                {
                    throw new Exception("Invalid node index");
                }

                var consensusNode = chain.ConsensusNodes[NodeIndex];
                var cts = new CancellationTokenSource();

                const string ROOT_PATH = @"C:\Users\harry\neoexpress";
                var path = Path.Combine(ROOT_PATH, consensusNode.Wallet.GetAccounts().Single(a => a.IsDefault).Address);

                if (Reset && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

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
            catch (Exception ex)
            {
                console.WriteLine(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
    }
}