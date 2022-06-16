using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.SmartContract;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using NeoTrace.Commands;
using OneOf;
using SysIO = System.IO;

namespace NeoTrace
{
    [Command("neotrace", Description = "Generates .neo-trace files for transactions on a public Neo N3 blockchains", UsePagerForHelpText = false)]
    [VersionOption(ThisAssembly.AssemblyInformationalVersion)]
    [Subcommand(typeof(BlockCommand), typeof(TransactionCommand))]
    class Program
    {
        public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify a subcommand.");
            app.ShowHelp(false);
            return 1;
        }

        static IReadOnlyList<string> mainNetRpcUris = new string[] {
            "http://seed1.neo.org:10332",
            "http://seed2.neo.org:10332",
            "http://seed3.neo.org:10332",
            "http://seed4.neo.org:10332",
            "http://seed5.neo.org:10332"
        };

        static IReadOnlyList<string> testNetRpcUris = new string[] {
            "http://seed1t5.neo.org:20332",
            "http://seed2t5.neo.org:20332",
            "http://seed3t5.neo.org:20332",
            "http://seed4t5.neo.org:20332",
            "http://seed5t5.neo.org:20332"
        };

        internal static Uri ParseRpcUri(string value)
        {
            if (string.IsNullOrEmpty(value)) return new Uri(mainNetRpcUris[0]);

            if (value.Equals("mainnet", StringComparison.InvariantCultureIgnoreCase)) return new Uri(mainNetRpcUris[0]);
            if (value.Equals("testnet", StringComparison.InvariantCultureIgnoreCase)) return new Uri(testNetRpcUris[0]);
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return uri;
            }

            throw new ArgumentException($"Invalid Neo RPC Uri {value}");
        }

        internal static async Task TraceBlockAsync(Uri uri, OneOf<uint, UInt256> blockId, IConsole console)
        {
            var settings = await GetProtocolSettingsAsync(uri).ConfigureAwait(false);

            using var rpcClient = new RpcClient(uri, protocolSettings: settings);
            var block = await GetBlockAsync(rpcClient, blockId).ConfigureAwait(false);
            if (block.Transactions.Length == 0) throw new Exception($"Block {block.Index} ({block.Hash}) had no transactions");

            await console.Out.WriteLineAsync($"Tracing all the transactions in block {block.Index} ({block.Hash})");
            TraceBlock(uri, block, settings, console);
        }

        internal static async Task TraceTransactionAsync(Uri uri, UInt256 txHash, IConsole console)
        {
            var settings = await GetProtocolSettingsAsync(uri).ConfigureAwait(false);

            using var rpcClient = new RpcClient(uri, protocolSettings: settings);
            var rpcTx = await rpcClient.GetRawTransactionAsync($"{txHash}").ConfigureAwait(false);
            var block = await GetBlockAsync(rpcClient, rpcTx.BlockHash).ConfigureAwait(false);
            await console.Out.WriteLineAsync($"Tracing transaction {txHash} in block {block.Index} ({block.Hash})");
            TraceBlock(uri, block, settings, console, rpcTx.Transaction.Hash);
        }

        static void TraceBlock(Uri uri, Block block, ProtocolSettings settings, IConsole console, UInt256? txHash = null)
        {
            IReadOnlyStore roStore = block.Index > 0
                ? new StateServiceStore(uri, block.Index - 1)
                : NullStore.Instance;

            using var store = new MemoryTrackingStore(roStore);
            using var snapshot = new SnapshotCache(store.GetSnapshot());

            using (var engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, block, settings, 0))
            {
                using var sb = new ScriptBuilder();
                sb.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
                engine.LoadScript(sb.ToArray());
                if (engine.Execute() != VMState.HALT) throw new InvalidOperationException("NativeOnPersist operation failed", engine.FaultException);
            }

            var clonedSnapshot = snapshot.CreateSnapshot();
            for (int i = 0; i < block.Transactions.Length; i++)
            {
                Transaction tx = block.Transactions[i];

                using var engine = GetEngine(tx, clonedSnapshot);
                if (engine is TraceApplicationEngine)
                {
                    console.Out.WriteLine($"Tracing Transaction #{i} ({tx.Hash})");
                }
                else
                {
                    console.Out.WriteLine($"Executing Transaction #{i} ({tx.Hash})");
                }

                engine.LoadScript(tx.Script);
                if (engine.Execute() == VMState.HALT)
                {
                    clonedSnapshot.Commit();
                }
                else
                {
                    clonedSnapshot = snapshot.CreateSnapshot();
                }
            }

            ApplicationEngine GetEngine(Transaction tx, DataCache snapshot)
            {
                if (txHash == null || txHash == tx.Hash)
                {
                    var path = SysIO.Path.Combine(Environment.CurrentDirectory, $"{tx.Hash}.neo-trace");
                    var sink = new TraceDebugStream(SysIO.File.OpenWrite(path));
                    return new TraceApplicationEngine(sink, TriggerType.Application, tx, snapshot, block, settings, tx.SystemFee);
                }
                else
                {
                    return ApplicationEngine.Create(TriggerType.Application, tx, snapshot, block, settings, tx.SystemFee);
                }
            }
        }

        static async Task<Block> GetBlockAsync(RpcClient rpcClient, OneOf<uint, UInt256> id)
        {
            var task = id.TryPickT0(out var index, out var hash)
                ? rpcClient.GetBlockHexAsync($"{index}")
                : rpcClient.GetBlockHexAsync($"{hash}");
            var hex = await task.ConfigureAwait(false);
            return Convert.FromBase64String(hex).AsSerializable<Block>();
        }

        static async Task<ProtocolSettings> GetProtocolSettingsAsync(Uri uri)
        {
            using var rpcClient = new RpcClient(uri);
            var result = await rpcClient.RpcSendAsync("getversion").ConfigureAwait(false);
            if (result["protocol"] == null)
            {
                var userAgent = result["useragent"].AsString();
                throw new NotSupportedException($"Trace not supported by {userAgent} running on {uri}");
            }

            var version = RpcVersion.FromJson(result);
            return ProtocolSettings.Default with
            {
                AddressVersion = version.Protocol.AddressVersion,
                InitialGasDistribution = version.Protocol.InitialGasDistribution,
                MaxTraceableBlocks = version.Protocol.MaxTraceableBlocks,
                MaxTransactionsPerBlock = version.Protocol.MaxTransactionsPerBlock,
                MemoryPoolMaxTransactions = version.Protocol.MemoryPoolMaxTransactions,
                MillisecondsPerBlock = version.Protocol.MillisecondsPerBlock,
                Network = version.Protocol.Network,
            };
        }
    }
}
