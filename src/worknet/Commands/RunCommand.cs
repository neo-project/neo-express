using System.IO.Abstractions;
using System.Net;
using Microsoft.Extensions.Configuration;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.Plugins;
using Neo.Cryptography.ECC;
using Neo.Persistence;
using Neo.Plugins;
using Neo.Wallets;
using NeoWorkNet.Models;

namespace NeoWorkNet.Commands;

[Command("run", Description = "Run Neo-WorkNet instance node")]
partial class RunCommand
{
    readonly IFileSystem fs;

    public RunCommand(IFileSystem fs)
    {
        this.fs = fs;
    }

    [Option(Description = "Time between blocks")]
    internal uint? SecondsPerBlock { get; }

    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
    {
        try
        {
            var (filename, worknet) = await fs.LoadWorknetAsync(app).ConfigureAwait(false);
            var dataDir = fs.GetWorknetDataDirectory(filename);
            if (!fs.Directory.Exists(dataDir)) throw new Exception($"Cannot locate data directory {dataDir}");

            var secondsPerBlock = SecondsPerBlock ?? 0;
            await RunAsync(worknet, dataDir, secondsPerBlock, console, token).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            app.WriteException(ex);
            return 1;
        }
    }

    static ProtocolSettings GetProtocolSettings(WorknetFile worknetFile, uint secondsPerBlock = 0)
    {
        var account = worknetFile.ConsensusWallet.GetAccounts().Single();
        var key = account.GetKey() ?? throw new Exception();
        return ProtocolSettings.Default with
        {
            Network = worknetFile.BranchInfo.Network,
            AddressVersion = worknetFile.BranchInfo.AddressVersion,
            MillisecondsPerBlock = secondsPerBlock == 0 ? 15000 : secondsPerBlock * 1000,
            ValidatorsCount = 1,
            StandbyCommittee = new ECPoint[] { key.PublicKey },
            SeedList = new string[] { $"{System.Net.IPAddress.Loopback}:{30333}" }
        };
    }

    static async Task RunAsync(WorknetFile worknet, string dataDir, uint secondsPerBlock, IConsole console, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>();
        _ = Task.Run(() =>
        {
            try
            {
                using var db = RocksDbUtility.OpenDb(dataDir);
                using var stateStore = new StateServiceStore(worknet.Uri, worknet.BranchInfo, db, true);
                using var trackStore = new PersistentTrackingStore(db, stateStore, true);

                var protocolSettings = GetProtocolSettings(worknet, secondsPerBlock);

                var storeProvider = new WorknetStorageProvider(trackStore);
                StoreFactory.RegisterProvider(storeProvider);

                using var persistencePlugin = new ToolkitPersistencePlugin(db);
                using var logPlugin = new WorkNetLogPlugin(console, Utility.GetDiagnosticWriter(console));
                using var dbftPlugin = new Neo.Consensus.DBFTPlugin(GetConsensusSettings(worknet));
                using var rpcServerPlugin = new WorknetRpcServerPlugin(GetRpcServerSettings(worknet), persistencePlugin, worknet.Uri);
                using var neoSystem = new NeoSystem(protocolSettings, storeProvider.Name);

                neoSystem.StartNode(new Neo.Network.P2P.ChannelsConfig
                {
                    Tcp = new IPEndPoint(IPAddress.Loopback, 30333),
                    WebSocket = new IPEndPoint(IPAddress.Loopback, 30334),
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

        static Neo.Consensus.Settings GetConsensusSettings(WorknetFile worknet)
        {
            var settings = new Dictionary<string, string>()
            {
                { "PluginConfiguration:Network", $"{worknet.BranchInfo.Network}" },
                { "IgnoreRecoveryLogs", "true" },
                { "RecoveryLogs", "ConsensusState" }
            };

            var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return new Neo.Consensus.Settings(config.GetSection("PluginConfiguration"));
        }

        static RpcServerSettings GetRpcServerSettings(WorknetFile worknet)
        {
            // var ipAddress = IPAddress.TryParse("0.0.0.0", out var _address) ? _address : IPAddress.Loopback;
            // chain.TryReadSetting<IPAddress>("rpc.BindAddress", IPAddress.TryParse, out var bindAddress)
            //     ? bindAddress : IPAddress.Loopback;

            var settings = new Dictionary<string, string>()
                {
                    { "PluginConfiguration:Network", $"{worknet.BranchInfo.Network}" },
                    { "PluginConfiguration:BindAddress", $"{IPAddress.Any}" },
                    { "PluginConfiguration:Port", $"{30332}" },
                    { "PluginConfiguration:SessionEnabled", $"{true}"}
                };

            var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return RpcServerSettings.Load(config.GetSection("PluginConfiguration"));
        }
    }
}
