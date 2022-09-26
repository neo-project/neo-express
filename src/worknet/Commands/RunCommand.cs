using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using Neo.Cryptography.ECC;
using Neo.Persistence;
using Neo.Wallets;
using NeoWorkNet.Models;
using System.IO.Abstractions;
using System.Net;

namespace NeoWorkNet.Commands;

[Command("run", Description = "")]
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
            // token = console.OverrideCancelKeyPress(token, true);

            var (fileName, worknet) = await fs.LoadWorknetAsync(app).ConfigureAwait(false);
            var dataDir = fs.Path.Combine(fs.Path.GetDirectoryName(fileName), "data");
            if (!fs.Directory.Exists(dataDir)) throw new Exception($"Cannot locate data directory {dataDir}");

            using var db = RocksDbUtility.OpenDb(dataDir);
            using var stateStore = new StateServiceStore(worknet.Uri, worknet.BranchInfo, db, true);
            using var trackStore = new PersistentTrackingStore(db, stateStore, true);
            var secondsPerBlock = SecondsPerBlock.HasValue ? SecondsPerBlock.Value : 0;

            await RunAsync(trackStore, worknet, secondsPerBlock, console, token).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            await app.Error.WriteLineAsync(ex.Message);
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

    static async Task RunAsync(IStore store, WorknetFile worknet, uint secondsPerBlock, IConsole console, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>();
        _ = Task.Run(() =>
        {
            try
            {
                var protocolSettings = GetProtocolSettings(worknet, secondsPerBlock);
                // var defaultAccount = node.Wallet.Accounts.Single(a => a.IsDefault);
                // using var mutex = new Mutex(true, GLOBAL_PREFIX + defaultAccount.ScriptHash);

                // var wallet = DevWallet.FromExpressWallet(ProtocolSettings, node.Wallet);
                // var multiSigAccount = wallet.GetMultiSigAccounts().Single();

                var storeProvider = new WorknetStorageProvider(store);
                StoreFactory.RegisterProvider(storeProvider);
                // // if (enableTrace) { Neo.SmartContract.ApplicationEngine.Provider = new ExpressApplicationEngineProvider(); }

                // using var persistencePlugin = new ExpressPersistencePlugin();
                using var logPlugin = new WorknetLogPlugin(console);
                using var dbftPlugin = new Neo.Consensus.DBFTPlugin(GetConsensusSettings(worknet));
                // using var rpcServerPlugin = new ExpressRpcServerPlugin(GetRpcServerSettings(chain, node),
                //     expressStorage, multiSigAccount.ScriptHash);
                using var neoSystem = new Neo.NeoSystem(protocolSettings, storeProvider.Name);

                neoSystem.StartNode(new Neo.Network.P2P.ChannelsConfig
                {
                    Tcp = new IPEndPoint(IPAddress.Loopback, 30333),
                    WebSocket = new IPEndPoint(IPAddress.Loopback, 30334),
                });
                dbftPlugin.Start(worknet.ConsensusWallet);

                // DevTracker looks for a string that starts with "Neo express is running" to confirm that the instance has started
                // Do not remove or re-word this console output:
                console.Out.WriteLine($"Neo worknet is running");

                // var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token, rpcServerPlugin.CancellationToken);
                token.WaitHandle.WaitOne();
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

    }
}
