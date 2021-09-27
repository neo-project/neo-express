using System;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.SmartContract;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using SysIO = System.IO;

namespace NeoTrace
{
    static class Extensions
    {
        public static async Task<Block> GetBlockAsync(this RpcClient rpcClient, uint index)
        {
            var hex = await rpcClient.GetBlockHexAsync($"{index}").ConfigureAwait(false);
            return Convert.FromBase64String(hex).AsSerializable<Block>();
        }
    }

    class Program
    {
        const string URL = "http://127.0.0.1:10332";
        const uint INDEX = 365110; //365228;

        static async Task Main(string[] args)
        {
            var url = new Uri(URL);
            var settings = await GetProtocolSettingsAsync(url).ConfigureAwait(false);

            using var rpcClient = new RpcClient(url, protocolSettings: settings);
            var block = await rpcClient.GetBlockAsync(INDEX).ConfigureAwait(false);

            using var store = new MemoryTrackingStore(new StateServiceStore(url, INDEX - 1));

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
                var path = SysIO.Path.Combine(Environment.CurrentDirectory, $"{tx.Hash}.neo-trace");
                var sink = new TraceDebugStream(SysIO.File.OpenWrite(path));
                
                using var engine = new TraceApplicationEngine(sink, TriggerType.Application, tx, clonedSnapshot, block, settings, tx.SystemFee);
                engine.LoadScript(tx.Script);
                var result = engine.Execute();
            }

            Console.WriteLine("Hello World!");
        }


        static async Task<ProtocolSettings> GetProtocolSettingsAsync(Uri uri)
        {
            using var rpcClient = new RpcClient(uri);
            var version = await rpcClient.GetVersionAsync().ConfigureAwait(false);
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
