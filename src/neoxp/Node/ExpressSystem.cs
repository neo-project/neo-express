using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
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
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Models;
using static Neo.Ledger.Blockchain;
using static Neo.SmartContract.Native.NativeContract;
using OracleRequest = Neo.SmartContract.Native.OracleRequest;

namespace NeoExpress.Node
{
    partial class ExpressSystem : IDisposable
    {
        public const string GLOBAL_PREFIX = "Global\\";
        const string APP_LOGS_STORE_NAME = "app-logs-store";
        const string NOTIFICATIONS_STORE_NAME = "notifications-store";

        [ThreadStatic]
        static Random? random;

        static Random Random
        {
            get
            {
                random ??= new Random();
                return random;
            }
        }

        readonly IExpressChain chain;
        readonly Lazy<KeyPair[]> consensusNodesKeys;
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

        public ProtocolSettings ProtocolSettings => neoSystem.Settings;
        // public Neo.Persistence.DataCache StoreView => neoSystem.StoreView;

        public ExpressSystem(IExpressChain chain, ExpressConsensusNode node, IExpressStorage expressStorage, bool trace, uint? secondsPerBlock)
        {
            this.chain = chain;
            this.node = node;
            this.expressStorage = expressStorage;

            appLogsStore = expressStorage.GetStore(APP_LOGS_STORE_NAME);
            notificationsStore = expressStorage.GetStore(NOTIFICATIONS_STORE_NAME);
            this.consensusNodesKeys = new Lazy<KeyPair[]>(() => chain.ConsensusNodes
                .Select(n => n.Wallet.DefaultAccount ?? throw new Exception($"{n.Wallet.Name} missing default account"))
                .Select(a => new KeyPair(a.PrivateKey.HexToBytes()))
                .ToArray());

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
            var wallet = DevWallet.FromExpressWallet(node.Wallet, neoSystem.Settings);
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
            await console.Out.WriteLineAsync($"Neo express is running ({expressStorage.Name})")
                .ConfigureAwait(false);

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

        static Lazy<byte[]> backwardsNotificationsPrefix = new Lazy<byte[]>(() =>
        {
            var buffer = new byte[sizeof(uint) + sizeof(ushort)];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, uint.MaxValue);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(sizeof(uint)), ushort.MaxValue);
            return buffer;
        });

        public RpcApplicationLog? GetAppLog(UInt256 hash)
        {
            var value = appLogsStore.TryGet(hash.ToArray());
            if (value is null || value.Length == 0) return null;
            var json = JObject.Parse(Neo.Utility.StrictUTF8.GetString(value));
            return json is not null
                ? RpcApplicationLog.FromJson(json, ProtocolSettings)
                : null;
        }

        public IEnumerable<(uint blockIndex, ushort txIndex, NotificationRecord notification)> GetNotifications(
            SeekDirection direction, IReadOnlySet<UInt160>? contracts, string eventName)
                => GetNotifications(direction, contracts,
                    string.IsNullOrEmpty(eventName) ? null : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { eventName });

        public IEnumerable<(uint blockIndex, ushort txIndex, NotificationRecord notification)> GetNotifications(
            SeekDirection direction = SeekDirection.Forward,
            IReadOnlySet<UInt160>? contracts = null,
            IReadOnlySet<string>? eventNames = null)
        {
            var prefix = direction == SeekDirection.Forward
                ? Array.Empty<byte>()
                : backwardsNotificationsPrefix.Value;

            return notificationsStore.Seek(prefix, direction)
                .Select(t => ParseNotification(t.Key, t.Value))
                .Where(t => contracts is null || contracts.Contains(t.notification.ScriptHash))
                .Where(t => eventNames is null || eventNames.Contains(t.notification.EventName));

            static (uint blockIndex, ushort txIndex, NotificationRecord notification) ParseNotification(byte[] key, byte[] value)
            {
                var blockIndex = BinaryPrimitives.ReadUInt32BigEndian(key.AsSpan(0, sizeof(uint)));
                var txIndex = BinaryPrimitives.ReadUInt16BigEndian(key.AsSpan(sizeof(uint), sizeof(ushort)));
                return (blockIndex, txIndex, notification: value.AsSerializable<NotificationRecord>());
            }
        }

        public void CreateCheckpoint(string path)
        {
            if (neoSystem.Settings.ValidatorsCount > 1)
            {
                throw new NotSupportedException("Checkpoint create is only supported on single node express instances");
            }

            if (expressStorage is RocksDbExpressStorage rocksDbExpressStorage)
            {
                var keys = chain.ConsensusNodes
                    .Select(n => n.Wallet.DefaultAccount ?? throw new Exception())
                    .Select(a => new KeyPair(Convert.FromHexString(a.PrivateKey)).PublicKey)
                    .ToArray();
                var contract = Neo.SmartContract.Contract.CreateMultiSigContract((keys.Length * 2 / 3) + 1, keys);
                rocksDbExpressStorage.CreateCheckpoint(path, neoSystem.Settings.Network, neoSystem.Settings.AddressVersion, contract.ScriptHash);
            }
            else
            {
                throw new NotSupportedException($"Checkpoint create is only supported for {nameof(RocksDbExpressStorage)}");
            }
        }

        public Block GetBlock(UInt256 blockHash)
            => Ledger.GetBlock(neoSystem.StoreView, blockHash);

        public Block GetBlock(uint blockIndex)
            => Ledger.GetBlock(neoSystem.StoreView, blockIndex);

        public ContractManifest GetContract(UInt160 scriptHash)
        {
            var contractState = ContractManagement.GetContract(neoSystem.StoreView, scriptHash);
            if (contractState is null) throw new Exception("Unknown contract");
            return contractState.Manifest;
        }

        public Transaction GetTransaction(UInt256 txHash)
        {
            return Ledger.GetTransaction(neoSystem.StoreView, txHash)
                ?? throw new Exception($"Unknown Transaction {txHash}");
        }

        public uint GetTransactionHeight(UInt256 txHash)
        {
            var txState = Ledger.GetTransactionState(neoSystem.StoreView, txHash)
                ?? throw new Exception($"Unknown Transaction {txHash}");
            return txState.BlockIndex;
        }

        public IEnumerable<(UInt160 hash, ContractManifest manifest)> ListContracts()
            => ContractManagement.ListContracts(neoSystem.StoreView)
                .OrderBy(c => c.Id)
                .Select(c => (c.Hash, c.Manifest));

        public IEnumerable<(ulong requestId, OracleRequest request)> ListOracleRequests()
            => Oracle.GetRequests(neoSystem.StoreView);

        public IEnumerable<(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value)> ListStorages(UInt160 scriptHash)
        {
            using var snapshot = neoSystem.GetSnapshot();
            var contract = ContractManagement.GetContract(snapshot, scriptHash);

            if (contract is null) return Array.Empty<(ReadOnlyMemory<byte>, ReadOnlyMemory<byte>)>();

            byte[] prefix = StorageKey.CreateSearchPrefix(contract.Id, default);
            return snapshot.Find(prefix).Select(t => (t.Key.Key, t.Value.Value));
        }

        public RpcInvokeResult Invoke(Neo.VM.Script script, Signer? signer = null)
        {
            using var snapshot = neoSystem.GetSnapshot();
            return Invoke(snapshot, script, signer);
        }

        RpcInvokeResult Invoke(DataCache snapshot, Neo.VM.Script script, Signer? signer = null)
        {
            var tx = new Transaction()
            {
                Nonce = (uint)Random.Next(),
                Script = script,
                Signers = signer is null ? Array.Empty<Signer>() : new Signer[1] { signer },
                Attributes = Array.Empty<TransactionAttribute>(),
                Witnesses = Array.Empty<Witness>(),
            };

            using var engine = ApplicationEngine.Run(script, snapshot,
                settings: ProtocolSettings,
                container: tx);

            return new RpcInvokeResult()
            {
                State = engine.State,
                Exception = engine.FaultException?.GetBaseException().Message ?? string.Empty,
                GasConsumed = engine.GasConsumed,
                Stack = engine.ResultStack.ToArray(),
                Script = string.Empty,
                Tx = string.Empty
            };
        }

        public (RpcNep17Balance balance, Nep17Contract token) GetBalance(UInt160 accountHash, UInt160 assetHash)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(assetHash, "balanceOf", accountHash);
            builder.EmitDynamicCall(assetHash, "symbol");
            builder.EmitDynamicCall(assetHash, "decimals");

            var result = Invoke(builder.ToArray());
            if (result.Stack.Length >= 3)
            {
                var balance = result.Stack[0].GetInteger();
                var symbol = result.Stack[1].GetString() ?? throw new Exception("invalid symbol");
                var decimals = (byte)result.Stack[2].GetInteger();

                return (
                    new RpcNep17Balance() { Amount = balance, AssetHash = assetHash },
                    new Nep17Contract(symbol, decimals, assetHash));
            }

            throw new Exception("invalid script results");
        }

        public IReadOnlyList<(TokenContract contract, BigInteger balance)> ListBalances(UInt160 address)
        {
            using var snapshot = neoSystem.GetSnapshot();
            var contracts = TokenContract.Enumerate(snapshot)
                .Where(c => c.standard == TokenStandard.Nep17)
                .ToList();

            var addressArray = address.ToArray();
            using var builder = new ScriptBuilder();
            for (var i = contracts.Count; i-- > 0;)
            {
                builder.EmitDynamicCall(contracts[i].scriptHash, "symbol");
                builder.EmitDynamicCall(contracts[i].scriptHash, "decimals");
                builder.EmitDynamicCall(contracts[i].scriptHash, "balanceOf", addressArray);
            }

            List<(TokenContract contract, BigInteger balance)> balances = new();
            using var engine = builder.Invoke(neoSystem.Settings, snapshot);
            if (engine.State != VMState.FAULT && engine.ResultStack.Count == contracts.Count * 3)
            {
                var resultStack = engine.ResultStack;
                for (var i = 0; i < contracts.Count; i++)
                {
                    var index = i * 3;
                    var symbol = resultStack.Peek(index + 2).GetString();
                    if (symbol is null) continue;
                    var decimals = (byte)resultStack.Peek(index + 1).GetInteger();
                    var balance = resultStack.Peek(index).GetInteger();
                    balances.Add((new TokenContract(symbol, decimals, contracts[i].scriptHash, contracts[i].standard), balance));
                }
            }

            return balances;
        }

        public IEnumerable<TokenContract> ListTokenContracts()
        {
            using var snapshot = neoSystem.GetSnapshot();
            return snapshot.EnumerateTokenContracts(neoSystem.Settings);
        }

        public Block GetLatestBlock()
        {
            using var snapshot = neoSystem.GetSnapshot();
            return GetLatestBlock(snapshot);
        }

        Block GetLatestBlock(DataCache snapshot)
        {
            var hash = Ledger.CurrentHash(snapshot);
            return Ledger.GetBlock(snapshot, hash);
        }

        public IReadOnlyList<ECPoint> ListOracleNodes()
        {
            using var snapshot = neoSystem.GetSnapshot();
            return ListOracleNodes(neoSystem.StoreView);
        }

        public IReadOnlyList<ECPoint> ListOracleNodes(DataCache snapshot)
        {
            var lastBlock = GetLatestBlock(snapshot);

            var role = new ContractParameter(ContractParameterType.Integer) { Value = (BigInteger)(byte)Neo.SmartContract.Native.Role.Oracle };
            var index = new ContractParameter(ContractParameterType.Integer) { Value = (BigInteger)lastBlock.Index + 1 };

            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(RoleManagement.Hash, "getDesignatedByRole", role, index);
            var result = Invoke(snapshot, builder.ToArray());

            if (result.State == Neo.VM.VMState.HALT
                && result.Stack.Length >= 1
                && result.Stack[0] is Neo.VM.Types.Array array)
            {
                var nodes = new ECPoint[array.Count];
                for (var x = 0; x < array.Count; x++)
                {
                    nodes[x] = ECPoint.DecodePoint(array[x].GetSpan(), ECCurve.Secp256r1);
                }
                return nodes;
            }

            return Array.Empty<ECPoint>();
        }

        public async Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Neo.VM.Script script, decimal additionalGas = 0)
        {
            var signer = new Signer() { Account = accountHash, Scopes = witnessScope };
            var (balance, _) = GetBalance(accountHash, GAS.Hash);
            var tx = wallet.MakeTransaction(neoSystem.StoreView, script, accountHash, new[] { signer }, maxGas: (long)balance.Amount);
            if (additionalGas > 0.0m)
            {
                tx.SystemFee += (long)additionalGas.ToBigInteger(GAS.Decimals);
            }

            var context = new ContractParametersContext(neoSystem.StoreView, tx, ProtocolSettings.Network);
            var account = wallet.GetAccount(accountHash) ?? throw new Exception();
            if (account.IsMultiSigContract())
            {

                var multiSigWallets = chain.GetMultiSigWallets(accountHash);
                for (int i = 0; i < multiSigWallets.Count; i++)
                {
                    multiSigWallets[i].Sign(context);
                    if (context.Completed) break;
                }
            }
            else
            {
                wallet.Sign(context);
            }

            if (!context.Completed)
            {
                throw new Exception();
            }

            tx.Witnesses = context.GetWitnesses();
            var blockHash = await SubmitTransactionAsync(tx).ConfigureAwait(false);
            return tx.Hash;
        }

        public async Task<UInt256> SubmitOracleResponseAsync(OracleResponse response)
        {
            using var snapshot = neoSystem.GetSnapshot();
            var height = Ledger.CurrentIndex(snapshot) + 1;
            var request = Oracle.GetRequest(snapshot, response.Id);
            var oracleNodes = ListOracleNodes(snapshot);
            // TODO: move NodeUtility methods over
            var tx = NodeUtility.CreateResponseTx(snapshot, request, response, oracleNodes, ProtocolSettings);
            if (tx is null) throw new Exception("Failed to create Oracle Response Tx");
            NodeUtility.SignOracleResponseTransaction(ProtocolSettings, chain, tx, oracleNodes);

            var blockHash = await SubmitTransactionAsync(tx);
            return tx.Hash;
        }

        public async Task FastForwardAsync(uint blockCount, TimeSpan timestampDelta)
        {
            if (timestampDelta.TotalSeconds < 0) throw new ArgumentException($"Negative {nameof(timestampDelta)} not supported");
            if (blockCount == 0) return;

            using var snapshot = neoSystem.GetSnapshot();
            var prevHash = Ledger.CurrentHash(snapshot);
            var prevHeader = Ledger.GetHeader(snapshot, prevHash);
            var timestamp = Math.Max(Neo.Helper.ToTimestampMS(DateTime.UtcNow), prevHeader.Timestamp + 1);
            var delta = (ulong)timestampDelta.TotalMilliseconds;

            if (blockCount == 1)
            {
                var block = NodeUtility.CreateSignedBlock(prevHeader, consensusNodesKeys.Value, chain.Network, timestamp: timestamp + delta);
                await RelayBlockAsync(block).ConfigureAwait(false);
            }
            else
            {
                var period = delta / (blockCount - 1);
                for (int i = 0; i < blockCount; i++)
                {
                    var block = NodeUtility.CreateSignedBlock(prevHeader, consensusNodesKeys.Value, chain.Network, timestamp: timestamp);
                    await RelayBlockAsync(block).ConfigureAwait(false);
                    prevHeader = block.Header;
                    timestamp += period;
                }
            }
        }


        public int PersistContract(ContractState state, IReadOnlyList<(string key, string value)> storagePairs, Commands.ContractCommand.OverwriteForce force)
        {
            if (chain.ConsensusNodes.Count != 1)
            {
                throw new ArgumentException("Contract download is only supported for single-node consensus");
            }

            // TODO: move this over
            return NodeUtility.PersistContract(neoSystem, state, storagePairs, force);
        }

        async Task<UInt256> SubmitTransactionAsync(Transaction tx)
        {
            var transactions = new[] { tx };

            // Verify the provided transactions. When running, Blockchain class does verification in two steps: VerifyStateIndependent and VerifyStateDependent.
            // However, Verify does both parts and there's no point in verifying dependent/independent in separate steps here
            var verificationContext = new TransactionVerificationContext();
            for (int i = 0; i < transactions.Length; i++)
            {
                if (transactions[i].Verify(neoSystem.Settings, neoSystem.StoreView, verificationContext) != VerifyResult.Succeed)
                {
                    throw new Exception("Verification failed");
                }
            }

            var prevHash = Ledger.CurrentHash(neoSystem.StoreView);
            var prevHeader = Ledger.GetHeader(neoSystem.StoreView, prevHash);
            // TODO: move CreateSignedBlock over from NodeUtility
            var block = NodeUtility.CreateSignedBlock(prevHeader,
                consensusNodesKeys.Value,
                neoSystem.Settings.Network,
                transactions);
            await RelayBlockAsync(block).ConfigureAwait(false);
            return block.Hash;
        }

        async Task RelayBlockAsync(Block block)
        {
            var blockRelay = await neoSystem.Blockchain.Ask<RelayResult>(block).ConfigureAwait(false);
            if (blockRelay.Result != VerifyResult.Succeed)
            {
                throw new Exception($"Block relay failed {blockRelay.Result}");
            }
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