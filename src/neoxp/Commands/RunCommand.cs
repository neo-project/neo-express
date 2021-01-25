using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Persistence;
using Neo.Persistence;

namespace NeoExpress.Commands
{
    [Command("run", Description = "Run Neo-Express instance node")]
    class RunCommand
    {
        [Argument(0, Description = "Index of node to run")]
        internal int NodeIndex { get; } = 0;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; } = string.Empty;

        [Option(Description = "Time between blocks")]
        internal uint SecondsPerBlock { get; }

        [Option(Description = "Discard blockchain changes on shutdown")]
        internal bool Discard { get; } = false;

        [Option(Description = "Enable contract execution tracing")]
        internal bool Trace { get; } = false;

        internal async Task<int> OnExecuteAsync(IFileSystem fileSystem, INodeManager nodeManager, IConsole console, CancellationToken token)
        {
            try
            {
                // var (chain, _) = Program.LoadExpressChain(Input);

                // if (NodeIndex < 0 || NodeIndex >= chain.ConsensusNodes.Count)
                // {
                //     throw new Exception("Invalid node index");
                // }

                // var node = chain.ConsensusNodes[NodeIndex];

                // if (node.IsRunning())
                // {
                //     throw new Exception("Node already running");
                // }

                // var folder = fileSystem.GetNodePath(node);
                // console.WriteLine(folder);

                // if (!nodeManager.InitializeProtocolSettings(chain, SecondsPerBlock))
                // {
                //     throw new Exception("could not initialize protocol settings");
                // }

                // using IStore store = GetStore(folder);
                // await nodeManager.RunAsync(store, node, Trace, console, token).ConfigureAwait(false);

                return 0;
            }
            catch (Exception ex)
            {
                await console.Error.WriteLineAsync(ex.Message);
                return 1;
            }
        }

        IStore GetStore(string folder)
        {
            if (Discard)
            {
                try
                {
                    var rocksDbStore = RocksDbStore.OpenReadOnly(folder);
                    return new CheckpointStore(rocksDbStore);
                }
                catch
                {
                    return new CheckpointStore(NullReadOnlyStore.Instance);
                }
            }
            else
            {
                return RocksDbStore.Open(folder);
            }
        }
    }
}
