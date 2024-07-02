// Copyright (C) 2015-2024 The Neo Project.
//
// Program.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
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


        internal static async Task TraceBlockAsync(Uri uri, OneOf<uint, UInt256> blockId, IConsole console)
        {
            var settings = await GetProtocolSettingsAsync(uri).ConfigureAwait(false);

            using var rpcClient = new RpcClient(uri, protocolSettings: settings);
            var block = await GetBlockAsync(rpcClient, blockId).ConfigureAwait(false);
            if (block.Transactions.Length == 0)
                throw new Exception($"Block {block.Index} ({block.Hash}) had no transactions");

            await console.Out.WriteLineAsync($"Tracing all the transactions in block {block.Index} ({block.Hash})").ConfigureAwait(false);
            await TraceBlockAsync(rpcClient, block, settings, console).ConfigureAwait(false);
        }

        internal static async Task TraceTransactionAsync(Uri uri, UInt256 txHash, IConsole console)
        {
            var settings = await GetProtocolSettingsAsync(uri).ConfigureAwait(false);

            using var rpcClient = new RpcClient(uri, protocolSettings: settings);
            var rpcTx = await rpcClient.GetRawTransactionAsync($"{txHash}").ConfigureAwait(false);
            var block = await GetBlockAsync(rpcClient, rpcTx.BlockHash).ConfigureAwait(false);
            await console.Out.WriteLineAsync($"Tracing transaction {txHash} in block {block.Index} ({block.Hash})").ConfigureAwait(false);
            await TraceBlockAsync(rpcClient, block, settings, console, rpcTx.Transaction.Hash).ConfigureAwait(false);
        }

        static async Task TraceBlockAsync(RpcClient rpcClient, Block block, ProtocolSettings settings, IConsole console, UInt256? txHash = null)
        {
            IReadOnlyStore roStore;
            if (block.Index == 0)
            {
                roStore = NullStore.Instance;
            }
            else
            {
                var branchInfo = await StateServiceStore.GetBranchInfoAsync(rpcClient, block.Index - 1).ConfigureAwait(false);
                roStore = new StateServiceStore(rpcClient, branchInfo);
            }

            using var store = new MemoryTrackingStore(roStore);
            using var snapshot = new SnapshotCache(store.GetSnapshot());

            using (var engine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshot, block, settings, 0))
            {
                using var sb = new ScriptBuilder();
                sb.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
                engine.LoadScript(sb.ToArray());
                if (engine.Execute() != VMState.HALT)
                    throw new InvalidOperationException("NativeOnPersist operation failed", engine.FaultException);
            }

            var clonedSnapshot = snapshot.CreateSnapshot();
            for (int i = 0; i < block.Transactions.Length; i++)
            {
                Transaction tx = block.Transactions[i];

                using var engine = GetEngine(tx, clonedSnapshot);
                if (engine is TraceApplicationEngine)
                {
                    await console.Out.WriteLineAsync($"Tracing Transaction #{i} ({tx.Hash})").ConfigureAwait(false);
                }
                else
                {
                    await console.Out.WriteLineAsync($"Executing Transaction #{i} ({tx.Hash})").ConfigureAwait(false);
                }

                var appLog = await rpcClient.GetApplicationLogAsync(tx.Hash.ToString()).ConfigureAwait(false);
                if (appLog.Executions.Count != 1)
                    throw new Exception($"Unexpected Application Log executions count. Expected 1, got {appLog.Executions.Count}");
                var execution = appLog.Executions[0];

                engine.LoadScript(tx.Script);
                engine.Execute();
                if (engine.State != execution.VMState)
                    throw new Exception($"Unexpected script execution state. Expected {execution.VMState} got {engine.State}");

                if (engine.State == VMState.HALT)
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
