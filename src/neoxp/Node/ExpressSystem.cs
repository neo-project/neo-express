using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Consensus;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.VM;
using NeoExpress.Models;
using static Neo.Ledger.Blockchain;
using static Neo.SmartContract.Native.NativeContract;

namespace NeoExpress.Node
{
    partial class ExpressSystem : IDisposable
    {
        public const string GLOBAL_PREFIX = "Global\\";
        const string APP_LOGS_STORE_NAME = "app-logs-store";
        const string NOTIFICATIONS_STORE_NAME = "notifications-store";

        readonly IExpressChain chain;
        readonly ExpressConsensusNode node;
        readonly IExpressStorage expressStorage;
        readonly IStore appLogsStore;
        readonly IStore notificationsStore;
        readonly NeoSystem neoSystem;
        readonly DBFTPlugin dbftPlugin;
        readonly CancellationTokenSource shutdownTokenSource = new();
        readonly string cacheId = DateTimeOffset.Now.Ticks.ToString();
        ISnapshot? appLogsSnapshot;
        ISnapshot? notificationsSnapshot;
        RpcServer? rpcServer;
        IWebHost? webHost;
        IConsole? console;

        // public ProtocolSettings ProtocolSettings => neoSystem.Settings;
        // public Neo.Persistence.DataCache StoreView => neoSystem.StoreView;

        public ExpressSystem(IExpressChain chain, ExpressConsensusNode node, IExpressStorage expressStorage, bool trace, uint? secondsPerBlock)
        {
            this.chain = chain;
            this.node = node;
            this.expressStorage = expressStorage;
            appLogsStore = expressStorage.GetStore(APP_LOGS_STORE_NAME);
            notificationsStore = expressStorage.GetStore(NOTIFICATIONS_STORE_NAME);

            var storeProvider = new StoreProvider(expressStorage);
            Neo.Persistence.StoreFactory.RegisterProvider(storeProvider);
            if (trace) { ApplicationEngine.Provider = new ApplicationEngineProvider(); }

            Blockchain.Committing += OnCommitting;
            Blockchain.Committed += OnCommitted;
            ApplicationEngine.Log += OnAppEngineLog;
            Neo.Utility.Logging += OnNeoUtilityLog;

            var protocolSettings = chain.GetProtocolSettings(secondsPerBlock);
            dbftPlugin = new DBFTPlugin(GetConsensusSettings(chain));
            neoSystem = new NeoSystem(protocolSettings, storeProvider.Name);

            static Neo.Consensus.Settings GetConsensusSettings(IExpressChain chain)
            {
                var settings = new Dictionary<string, string>()
                {
                    { "PluginConfiguration:Network", $"{chain.Network}" },
                    { "IgnoreRecoveryLogs", "true" }
                };

                var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
                return new Neo.Consensus.Settings(config.GetSection("PluginConfiguration"));
            }
        }

        public void Dispose()
        {
            Blockchain.Committing -= OnCommitting;
            Blockchain.Committed -= OnCommitted;
            ApplicationEngine.Log -= OnAppEngineLog;
            Neo.Utility.Logging -= OnNeoUtilityLog;

            webHost?.Dispose();
            rpcServer?.Dispose();
            neoSystem.Dispose();
            expressStorage.Dispose();
        }

        public async Task RunAsync(IConsole console, CancellationToken token)
        {
            if (node.IsRunning()) { throw new Exception("Node already running"); }

            this.console = console;
            var rpcSettings = GetRpcServerSettings(chain, node);
            rpcServer = new RpcServer(neoSystem, rpcSettings);
            rpcServer.RegisterMethods(this);
            webHost = BuildWebHost(rpcServer, rpcSettings);

            var defaultAccount = node.Wallet.Accounts.Single(a => a.IsDefault);
            var wallet = DevWallet.FromExpressWallet(neoSystem.Settings, node.Wallet);
            var multiSigAccount = wallet.GetMultiSigAccounts().Single();

            using var mutex = new Mutex(true, GLOBAL_PREFIX + defaultAccount.ScriptHash);

            neoSystem.StartNode(new Neo.Network.P2P.ChannelsConfig
            {
                Tcp = new IPEndPoint(IPAddress.Loopback, node.TcpPort),
                WebSocket = new IPEndPoint(IPAddress.Loopback, node.WebSocketPort),
            });
            dbftPlugin.Start(wallet);

            // DevTracker looks for a string that starts with "Neo express is running" to confirm that the instance has started
            // Do not remove or re-word this console output:
            console.Out.WriteLine($"Neo express is running ({expressStorage.Name})");

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token, shutdownTokenSource.Token);

            var tcs = new TaskCompletionSource<bool>();
            _ = Task.Run(() =>
            {
                try
                {
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
            });
            await tcs.Task.ConfigureAwait(false);
        }

        void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            appLogsSnapshot?.Dispose();
            notificationsSnapshot?.Dispose();

            if (applicationExecutedList.Count > ushort.MaxValue) throw new Exception("applicationExecutedList too big");

            appLogsSnapshot = appLogsStore.GetSnapshot();
            notificationsSnapshot = notificationsStore.GetSnapshot();

            var notificationIndex = new byte[sizeof(uint) + (2 * sizeof(ushort))];
            BinaryPrimitives.WriteUInt32BigEndian(
                notificationIndex.AsSpan(0, sizeof(uint)),
                block.Index);

            for (int i = 0; i < applicationExecutedList.Count; i++)
            {
                ApplicationExecuted appExec = applicationExecutedList[i];
                if (appExec.Transaction is null) continue;

                // log TX faults to the console
                if (appExec.VMState == Neo.VM.VMState.FAULT
                    && console is not null)
                {
                    var logMessage = $"Tx FAULT: hash={appExec.Transaction.Hash}";
                    if (!string.IsNullOrEmpty(appExec.Exception.Message))
                    {
                        logMessage += $" exception=\"{appExec.Exception.Message}\"";
                    }
                    console.Error.WriteLine($"\x1b[31m{logMessage}\x1b[0m");
                }

                var txJson = TxLogToJson(appExec);
                appLogsSnapshot.Put(appExec.Transaction.Hash.ToArray(), Neo.Utility.StrictUTF8.GetBytes(txJson.ToString()));

                if (appExec.VMState != VMState.FAULT)
                {
                    if (appExec.Notifications.Length > ushort.MaxValue) throw new Exception("appExec.Notifications too big");

                    BinaryPrimitives.WriteUInt16BigEndian(notificationIndex.AsSpan(sizeof(uint), sizeof(ushort)), (ushort)i);

                    for (int j = 0; j < appExec.Notifications.Length; j++)
                    {
                        BinaryPrimitives.WriteUInt16BigEndian(
                            notificationIndex.AsSpan(sizeof(uint) + sizeof(ushort), sizeof(ushort)),
                            (ushort)j);
                        var record = new NotificationRecord(appExec.Notifications[j]);
                        notificationsSnapshot.Put(notificationIndex.ToArray(), record.ToArray());
                    }
                }
            }

            var blockJson = BlockLogToJson(block, applicationExecutedList);
            if (blockJson is not null)
            {
                appLogsSnapshot.Put(block.Hash.ToArray(), Neo.Utility.StrictUTF8.GetBytes(blockJson.ToString()));
            }
        }

        private void OnCommitted(NeoSystem system, Block block)
        {
            appLogsSnapshot?.Commit();
            notificationsSnapshot?.Commit();
        }

        void OnNeoUtilityLog(string source, LogLevel level, object message)
        {
            if (console is null) return;
            console.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.ff} {source} {level} {message}");
        }

        void OnAppEngineLog(object? sender, LogEventArgs args)
        {
            if (console is null) return;
            var container = args.ScriptContainer is null
                ? string.Empty
                : $" [{args.ScriptContainer.GetType().Name}]";
            var contractName = (neoSystem is not null
                ? ContractManagement.GetContract(neoSystem.StoreView, args.ScriptHash)?.Manifest.Name
                : null) ?? args.ScriptHash.ToString();
            console.WriteLine($"\x1b[35m{contractName}\x1b[0m Log: \x1b[96m\"{args.Message}\"\x1b[0m{container}");
        }

        static IWebHost BuildWebHost(RpcServer rpcServer, RpcServerSettings settings)
        {
            var builder = new WebHostBuilder();
            builder.UseKestrel(options =>
            {
                options.Listen(settings.BindAddress, settings.Port);
            });
            builder.Configure(app =>
            {
                app.UseResponseCompression();
                app.Run(rpcServer.ProcessAsync);
            });
            builder.ConfigureServices(services =>
            {
                services.AddResponseCompression(options =>
                {
                    options.Providers.Add<GzipCompressionProvider>();
                    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append("application/json");
                });

                services.Configure<GzipCompressionProviderOptions>(options =>
                {
                    options.Level = System.IO.Compression.CompressionLevel.Fastest;
                });
            });

            var host = builder.Build();
            host.Start();
            return host;
        }

        static RpcServerSettings GetRpcServerSettings(IExpressChain chain, ExpressConsensusNode node)
        {
            var ipAddress = chain.TryReadSetting<IPAddress>("rpc.BindAddress", IPAddress.TryParse, out var bindAddress)
                ? bindAddress : IPAddress.Loopback;

            var settings = new Dictionary<string, string>()
                {
                    { "PluginConfiguration:Network", $"{chain.Network}" },
                    { "PluginConfiguration:BindAddress", $"{ipAddress}" },
                    { "PluginConfiguration:Port", $"{node.RpcPort}" }
                };

            if (chain.TryReadSetting<decimal>("rpc.MaxGasInvoke", decimal.TryParse, out var maxGasInvoke))
            {
                settings.Add("PluginConfiguration:MaxGasInvoke", $"{maxGasInvoke}");
            }

            if (chain.TryReadSetting<decimal>("rpc.MaxFee", decimal.TryParse, out var maxFee))
            {
                settings.Add("PluginConfiguration:MaxFee", $"{maxFee}");
            }

            if (chain.TryReadSetting<int>("rpc.MaxIteratorResultItems", int.TryParse, out var maxIteratorResultItems)
                && maxIteratorResultItems > 0)
            {
                settings.Add("PluginConfiguration:MaxIteratorResultItems", $"{maxIteratorResultItems}");
            }

            var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return Neo.Plugins.RpcServerSettings.Load(config.GetSection("PluginConfiguration"));
        }

        // TxLogToJson and BlockLogToJson copied from Neo.Plugins.LogReader in the ApplicationLogs plugin
        // to avoid dependency on LevelDBStore package

        static JObject TxLogToJson(ApplicationExecuted appExec)
        {
            global::System.Diagnostics.Debug.Assert(appExec.Transaction is not null);

            var txJson = new JObject();
            txJson["txid"] = appExec.Transaction.Hash.ToString();
            JObject trigger = new JObject();
            trigger["trigger"] = appExec.Trigger;
            trigger["vmstate"] = appExec.VMState;
            trigger["exception"] = GetExceptionMessage(appExec.Exception);
            trigger["gasconsumed"] = appExec.GasConsumed.ToString();
            try
            {
                trigger["stack"] = appExec.Stack.Select(q => q.ToJson()).ToArray();
            }
            catch (InvalidOperationException)
            {
                trigger["stack"] = "error: recursive reference";
            }
            trigger["notifications"] = appExec.Notifications.Select(q =>
            {
                JObject notification = new JObject();
                notification["contract"] = q.ScriptHash.ToString();
                notification["eventname"] = q.EventName;
                try
                {
                    notification["state"] = q.State.ToJson();
                }
                catch (InvalidOperationException)
                {
                    notification["state"] = "error: recursive reference";
                }
                return notification;
            }).ToArray();

            txJson["executions"] = new List<JObject>() { trigger }.ToArray();
            return txJson;

            static string? GetExceptionMessage(Exception exception)
            {
                return exception?.GetBaseException().Message;
            }
        }

        static JObject? BlockLogToJson(Block block, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            var blocks = applicationExecutedList.Where(p => p.Transaction is null).ToArray();
            if (blocks.Length > 0)
            {
                var blockJson = new JObject();
                var blockHash = block.Hash.ToArray();
                blockJson["blockhash"] = block.Hash.ToString();
                var triggerList = new List<JObject>();
                foreach (var appExec in blocks)
                {
                    JObject trigger = new JObject();
                    trigger["trigger"] = appExec.Trigger;
                    trigger["vmstate"] = appExec.VMState;
                    trigger["gasconsumed"] = appExec.GasConsumed.ToString();
                    try
                    {
                        trigger["stack"] = appExec.Stack.Select(q => q.ToJson()).ToArray();
                    }
                    catch (InvalidOperationException)
                    {
                        trigger["stack"] = "error: recursive reference";
                    }
                    trigger["notifications"] = appExec.Notifications.Select(q =>
                    {
                        JObject notification = new JObject();
                        notification["contract"] = q.ScriptHash.ToString();
                        notification["eventname"] = q.EventName;
                        try
                        {
                            notification["state"] = q.State.ToJson();
                        }
                        catch (InvalidOperationException)
                        {
                            notification["state"] = "error: recursive reference";
                        }
                        return notification;
                    }).ToArray();
                    triggerList.Add(trigger);
                }
                blockJson["executions"] = triggerList.ToArray();
                return blockJson;
            }

            return null;
        }
    }
}