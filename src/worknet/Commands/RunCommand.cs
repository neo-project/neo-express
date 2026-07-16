// Copyright (C) 2015-2026 The Neo Project.
//
// RunCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.Plugins;
using Neo.Cryptography.ECC;
using Neo.Persistence;
using Neo.Plugins;
using Neo.Plugins.DBFTPlugin;
using Neo.Plugins.RpcServer;
using NeoWorkNet.Models;
using System.IO.Abstractions;
using System.Net;

namespace NeoWorkNet.Commands;

[Command("run", Description = "Run Neo-WorkNet instance node")]
partial class RunCommand
{
    internal const ushort DEFAULT_RPC_PORT = 30332;
    internal const ushort DEFAULT_TCP_PORT = 30333;

    readonly IFileSystem fs;

    public RunCommand(IFileSystem fs)
    {
        this.fs = fs;
    }

    [Option(Description = "Time between blocks")]
    internal uint? SecondsPerBlock { get; }

    [Option("--rpc-port <PORT>", Description = "RPC server port")]
    internal ushort RpcPort { get; } = DEFAULT_RPC_PORT;

    [Option("--tcp-port <PORT>", Description = "TCP server port")]
    internal ushort TcpPort { get; } = DEFAULT_TCP_PORT;

    [Option("--disable-log", Description = "Disable verbose data logging")]
    internal bool DisableLog { get; set; }

    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
    {
        try
        {
            var (filename, worknet) = await fs.LoadWorknetAsync(app).ConfigureAwait(false);
            var dataDir = fs.GetWorknetDataDirectory(filename);
            if (!fs.Directory.Exists(dataDir))
                throw new Exception($"Cannot locate data directory {dataDir}");

            ValidatePorts(RpcPort, TcpPort);

            var secondsPerBlock = SecondsPerBlock ?? 0;
            await RunAsync(worknet, dataDir, secondsPerBlock, RpcPort, TcpPort, DisableLog, console, token).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            app.WriteException(ex);
            return 1;
        }
    }

    internal static ProtocolSettings GetProtocolSettings(WorknetFile worknetFile, uint secondsPerBlock = 0, ushort tcpPort = DEFAULT_TCP_PORT)
    {
        var account = worknetFile.ConsensusWallet.GetAccounts().Single();
        var key = account.GetKey() ?? throw new Exception();
        return worknetFile.BranchInfo.ProtocolSettings with
        {
            MillisecondsPerBlock = secondsPerBlock == 0 ? 15000 : secondsPerBlock * 1000,
            ValidatorsCount = 1,
            StandbyCommittee = new ECPoint[] { key.PublicKey },
            SeedList = new string[] { $"{System.Net.IPAddress.Loopback}:{tcpPort}" }
        };
    }

    internal static RpcServersSettings GetRpcServerSettings(WorknetFile worknet, ushort rpcPort)
    {
        ValidatePort(rpcPort, nameof(rpcPort));

        var settings = new Dictionary<string, string>()
            {
                { "PluginConfiguration:Network", $"{worknet.BranchInfo.Network}" },
                { "PluginConfiguration:BindAddress", $"{IPAddress.Loopback}" },
                { "PluginConfiguration:Port", $"{rpcPort}" },
                { "PluginConfiguration:SessionEnabled", $"{true}"}
            };

        var config = new ConfigurationBuilder().AddInMemoryCollection(settings!).Build();
        return RpcServersSettings.Load(config.GetSection("PluginConfiguration"));
    }

    internal static DbftSettings GetConsensusSettings(WorknetFile worknet)
    {
        var settings = new Dictionary<string, string>()
        {
            { "PluginConfiguration:Network", $"{worknet.BranchInfo.Network}" },
            { "PluginConfiguration:IgnoreRecoveryLogs", "true" },
            { "PluginConfiguration:RecoveryLogs", "ConsensusState" }
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(settings!).Build();
        return new DbftSettings(config.GetSection("PluginConfiguration"));
    }

    static async Task RunAsync(WorknetFile worknet, string dataDir, uint secondsPerBlock, ushort rpcPort, ushort tcpPort, bool disableLog, IConsole console, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>();
        _ = Task.Run(() =>
        {
            try
            {
                using var db = RocksDbUtility.OpenDb(dataDir);
                using var stateStore = new StateServiceStore(worknet.Uri, worknet.BranchInfo, db, true);
                using var trackStore = new PersistentTrackingStore(db, stateStore, true);

                var protocolSettings = GetProtocolSettings(worknet, secondsPerBlock, tcpPort);

                var storeProvider = new WorknetStorageProvider(trackStore);
                StoreFactory.RegisterProvider(storeProvider);

                using var persistencePlugin = new ToolkitPersistencePlugin(db);
                using var logPlugin = new WorkNetLogPlugin(console,
                    disableLog ? null : Utility.GetDiagnosticWriter(console));
                using var dbftPlugin = new DBFTPlugin(GetConsensusSettings(worknet));
                using var rpcServerPlugin = new WorknetRpcServerPlugin(GetRpcServerSettings(worknet, rpcPort), persistencePlugin, worknet.Uri);
                using var neoSystem = new NeoSystem(protocolSettings, storeProvider.Name);

                neoSystem.StartNode(new Neo.Network.P2P.ChannelsConfig
                {
                    Tcp = new IPEndPoint(IPAddress.Loopback, tcpPort)
                });
                dbftPlugin.Start(worknet.ConsensusWallet);

                // DevTracker looks for a string that starts with "Neo express is running" to confirm that the instance has started
                // Do not remove or re-word this console output:
                console.Out.WriteLine($"Neo worknet is running");

                var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token, rpcServerPlugin.CancellationToken);
                linkedToken.Token.WaitHandle.WaitOne();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
            finally
            {
                tcs.TrySetResult(true);
            }
        }, CancellationToken.None);
        await tcs.Task.ConfigureAwait(false);
    }

    static void ValidatePort(ushort port, string name)
    {
        if (port == 0)
            throw new ArgumentOutOfRangeException(name, "Port must be greater than zero.");
    }

    internal static void ValidatePorts(ushort rpcPort, ushort tcpPort)
    {
        ValidatePort(rpcPort, nameof(rpcPort));
        ValidatePort(tcpPort, nameof(tcpPort));
        if (rpcPort == tcpPort)
            throw new ArgumentException("RPC and TCP ports must be different.");
    }
}
