using McMaster.Extensions.CommandLineUtils;
using Neo.Plugins;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using Neo.Express.Persistence;
using System.ComponentModel.DataAnnotations;

namespace Neo.Express.Commands
{
    [Command("run")]
    internal class RunCommand
    {
        [Argument(0)]
        [Required]
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

        public static CancellationTokenSource Run(Neo.Persistence.Store store, DevConsensusNode consensusNode, IConsole console)
        {
            var cts = new CancellationTokenSource();

            Task.Factory.StartNew(() =>
            {
                try
                {
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
                finally
                {
                    if (store is IDisposable disp)
                    {
                        disp.Dispose();
                    }
                }
            });

            return cts;
        }

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var devChain = DevChain.Initialize(Input, SecondsPerBlock);
                if (NodeIndex >= devChain.ConsensusNodes.Count || NodeIndex < 0)
                {
                    throw new Exception("Invalid node index");
                }

                var consensusNode = devChain.ConsensusNodes[NodeIndex];
                var blockchainPath = consensusNode.BlockchainPath;

                if (Reset && Directory.Exists(blockchainPath))
                {
                    Directory.Delete(blockchainPath, true);
                }

                if (!Directory.Exists(blockchainPath))
                {
                    Directory.CreateDirectory(blockchainPath);
                }

                var cts = Run(new RocksDbStore(blockchainPath), consensusNode, console);
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