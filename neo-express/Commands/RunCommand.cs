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

        [Option(ShortName = "")]
        private bool Reset { get; }

        [Option]
        private bool ReadOnly { get; }

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
                if (ReadOnly && Reset)
                {
                    throw new Exception("Cannot specify --reset and --read-only");
                }

                var (devChainJson, _) = DevChain.LoadJson(Input);
                if (!DevChain.InitializeProtocolSettings(devChainJson, SecondsPerBlock))
                {
                    throw new Exception("Couldn't initialize protocol settings");
                }

                var devChain = DevChain.FromJson(devChainJson);
                if (NodeIndex >= devChain.ConsensusNodes.Count || NodeIndex < 0)
                {
                    throw new Exception("Invalid node index");
                }

                var consensusNode = devChain.ConsensusNodes[NodeIndex];
                var cts = new CancellationTokenSource();

                var blockchainPath = consensusNode.BlockchainPath;

                if (Reset && Directory.Exists(blockchainPath))
                {
                    Directory.Delete(blockchainPath, true);
                }

                if (!Directory.Exists(blockchainPath))
                {
                    Directory.CreateDirectory(blockchainPath);
                }

                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        using (var store = new RocksDbStore(blockchainPath))
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