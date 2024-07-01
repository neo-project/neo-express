// Copyright (C) 2015-2024 The Neo Project.
//
// OfflineNode.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using NeoExpress.Commands;
using NeoExpress.Models;
using NeoExpress.Validators;
using System.Numerics;
using static Neo.Ledger.Blockchain;

namespace NeoExpress.Node
{
    sealed class OfflineNode : IExpressNode
    {
        readonly NeoSystem neoSystem;
        readonly Wallet nodeWallet;
        readonly ExpressChain chain;
        readonly RocksDbExpressStorage expressStorage;
        readonly ExpressPersistencePlugin persistencePlugin;
        readonly Lazy<KeyPair[]> consensusNodesKeys;
        bool disposedValue;

        public ProtocolSettings ProtocolSettings => neoSystem.Settings;

        public OfflineNode(ProtocolSettings settings, RocksDbExpressStorage expressStorage, ExpressWallet nodeWallet, ExpressChain chain, bool enableTrace)
        {
            this.nodeWallet = DevWallet.FromExpressWallet(settings, nodeWallet);
            this.chain = chain;
            this.expressStorage = expressStorage;
            consensusNodesKeys = new Lazy<KeyPair[]>(() => chain.GetConsensusNodeKeys());

            var storeProvider = new ExpressStoreProvider(expressStorage);
            StoreFactory.RegisterProvider(storeProvider);
            if (enableTrace)
            { ApplicationEngine.Provider = new ExpressApplicationEngineProvider(); }

            persistencePlugin = new ExpressPersistencePlugin();
            neoSystem = new NeoSystem(settings, storeProvider.Name);

            ApplicationEngine.Log += OnLog!;
        }

        public void Dispose()
        {
            if (!disposedValue)
            {
                ApplicationEngine.Log -= OnLog!;
                persistencePlugin.Dispose();
                neoSystem.Dispose();
                disposedValue = true;
            }
        }

        private void OnLog(object sender, LogEventArgs args)
        {
            var engine = sender as ApplicationEngine;
            var tx = engine?.ScriptContainer as Transaction;
            var colorCode = tx?.Witnesses?.Any() ?? false ? "96" : "93";

            var contract = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, args.ScriptHash);
            var name = contract is null ? args.ScriptHash.ToString() : contract.Manifest.Name;
            Console.WriteLine($"\x1b[35m{name}\x1b[0m Log: \x1b[{colorCode}m\"{args.Message}\"\x1b[0m [{args.ScriptContainer.GetType().Name}]");
        }

        Task<T> MakeAsync<T>(Func<T> func)
        {
            try
            {
                if (disposedValue)
                    throw new ObjectDisposedException(nameof(OfflineNode));
                return Task.FromResult(func());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        IExpressNode.CheckpointMode CreateCheckpoint(string checkPointPath)
        {
            var multiSigAccount = nodeWallet.GetMultiSigAccounts().Single();
            expressStorage.CreateCheckpoint(checkPointPath, ProtocolSettings.Network, ProtocolSettings.AddressVersion, multiSigAccount.ScriptHash);
            return IExpressNode.CheckpointMode.Offline;
        }

        public Task<IExpressNode.CheckpointMode> CreateCheckpointAsync(string checkPointPath)
            => MakeAsync(() => CreateCheckpoint(checkPointPath));

        RpcInvokeResult Invoke(Neo.VM.Script script, Signer? signer = null)
        {
            var tx = TestApplicationEngine.CreateTestTransaction(signer);
            using var engine = script.Invoke(neoSystem.Settings, neoSystem.StoreView, tx);

            return new RpcInvokeResult()
            {
                State = engine.State,
                Exception = engine.FaultException?.GetBaseException().Message ?? string.Empty,
                GasConsumed = engine.FeeConsumed,
                Stack = engine.ResultStack.ToArray(),
                Script = string.Empty,
                Tx = string.Empty
            };
        }

        public Task<RpcInvokeResult> InvokeAsync(Neo.VM.Script script, Signer? signer = null)
            => MakeAsync(() => Invoke(script, signer));

        public async Task<UInt256> ExecuteAsync(Wallet wallet, UInt160 accountHash, WitnessScope witnessScope, Neo.VM.Script script, decimal additionalGas = 0)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(OfflineNode));

            var signer = new Signer() { Account = accountHash, Scopes = witnessScope };
            var (balance, _) = await this.GetBalanceAsync(accountHash, "GAS");
            var tx = wallet.MakeTransaction(neoSystem.StoreView, script, accountHash, new[] { signer }, maxGas: (long)balance.Amount);
            if (additionalGas > 0.0m)
            {
                tx.SystemFee += (long)additionalGas.ToBigInteger(NativeContract.GAS.Decimals);
            }

            var context = new ContractParametersContext(neoSystem.StoreView, tx, ProtocolSettings.Network);
            var account = wallet.GetAccount(accountHash) ?? throw new Exception();
            if (account.IsMultiSigContract())
            {
                var multiSigWallets = chain.GetMultiSigWallets(neoSystem.Settings, accountHash);
                for (int i = 0; i < multiSigWallets.Count; i++)
                {
                    multiSigWallets[i].Sign(context);
                    if (context.Completed)
                        break;
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

        public async Task<UInt256> SubmitOracleResponseAsync(OracleResponse response, IReadOnlyList<ECPoint> oracleNodes)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(OfflineNode));

            using var snapshot = neoSystem.GetSnapshot();
            var height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            var request = NativeContract.Oracle.GetRequest(snapshot, response.Id);
            var tx = NodeUtility.CreateResponseTx(snapshot, request, response, oracleNodes, ProtocolSettings);
            if (tx is null)
                throw new Exception("Failed to create Oracle Response Tx");
            NodeUtility.SignOracleResponseTransaction(ProtocolSettings, chain, tx, oracleNodes);

            var blockHash = await SubmitTransactionAsync(tx);
            return tx.Hash;
        }

        public async Task FastForwardAsync(uint blockCount, TimeSpan timestampDelta)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(OfflineNode));

            var prevHash = NativeContract.Ledger.CurrentHash(neoSystem.StoreView);
            var prevHeader = NativeContract.Ledger.GetHeader(neoSystem.StoreView, prevHash);

            await NodeUtility.FastForwardAsync(prevHeader,
                blockCount,
                timestampDelta,
                consensusNodesKeys.Value,
                ProtocolSettings.Network,
                block => RelayBlockAsync(block));
        }

        async Task<UInt256> SubmitTransactionAsync(Transaction tx)
        {
            if (disposedValue)
                throw new ObjectDisposedException(nameof(OfflineNode));

            var transactions = new[] { tx };

            // Verify the provided transactions. When running, Blockchain class does verification in two steps: VerifyStateIndependent and VerifyStateDependent.
            // However, Verify does both parts and there's no point in verifying dependent/independent in separate steps here
            var verificationContext = new TransactionVerificationContext();
            for (int i = 0; i < transactions.Length; i++)
            {
                if (transactions[i].Verify(neoSystem.Settings, neoSystem.StoreView, verificationContext, new List<Transaction>()) != VerifyResult.Succeed)
                {
                    throw new Exception("Verification failed");
                }
            }

            var prevHash = NativeContract.Ledger.CurrentHash(neoSystem.StoreView);
            var prevHeader = NativeContract.Ledger.GetHeader(neoSystem.StoreView, prevHash);
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

        public Task<Block> GetBlockAsync(UInt256 blockHash)
            => MakeAsync(() => NativeContract.Ledger.GetBlock(neoSystem.StoreView, blockHash));

        public Task<Block> GetBlockAsync(uint blockIndex)
            => MakeAsync(() => NativeContract.Ledger.GetBlock(neoSystem.StoreView, blockIndex));

        ContractManifest GetContract(UInt160 scriptHash)
        {
            var contractState = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, scriptHash);
            if (contractState is null)
                throw new Exception("Unknown contract");
            return contractState.Manifest;
        }

        public Task<ContractManifest> GetContractAsync(UInt160 scriptHash)
            => MakeAsync(() => GetContract(scriptHash));

        Block GetLatestBlock()
        {
            using var snapshot = neoSystem.GetSnapshot();
            var hash = NativeContract.Ledger.CurrentHash(snapshot);
            return NativeContract.Ledger.GetBlock(snapshot, hash);
        }

        public Task<Block> GetLatestBlockAsync() => MakeAsync(GetLatestBlock);

        (Transaction tx, RpcApplicationLog? appLog) GetTransaction(UInt256 txHash)
        {
            var tx = NativeContract.Ledger.GetTransaction(neoSystem.StoreView, txHash);
            if (tx is null)
                throw new Exception("Unknown Transaction");

            var jsonLog = persistencePlugin.GetAppLog(txHash);
            return jsonLog is not null
                ? (tx, RpcApplicationLog.FromJson(jsonLog, ProtocolSettings))
                : (tx, null);
        }

        public Task<(Transaction tx, RpcApplicationLog? appLog)> GetTransactionAsync(UInt256 txHash)
            => MakeAsync(() => GetTransaction(txHash));

        uint GetTransactionHeight(UInt256 txHash)
        {
            var height = NativeContract.Ledger.GetTransactionState(neoSystem.StoreView, txHash)?.BlockIndex;
            return height.HasValue
                ? height.Value
                : throw new Exception("Unknown Transaction");
        }

        public Task<uint> GetTransactionHeightAsync(UInt256 txHash)
            => MakeAsync(() => GetTransactionHeight(txHash));

        IReadOnlyList<(TokenContract contract, BigInteger balance)> ListBalances(UInt160 address)
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
                    if (symbol is null)
                        continue;
                    var decimals = (byte)resultStack.Peek(index + 1).GetInteger();
                    var balance = resultStack.Peek(index).GetInteger();
                    balances.Add((new TokenContract(symbol, decimals, contracts[i].scriptHash, contracts[i].standard), balance));
                }
            }

            return balances;
        }

        public Task<IReadOnlyList<(TokenContract contract, BigInteger balance)>> ListBalancesAsync(UInt160 address)
            => MakeAsync(() => ListBalances(address));

        IReadOnlyList<(UInt160 hash, ContractManifest manifest)> ListContracts()
        {
            return NativeContract.ContractManagement.ListContracts(neoSystem.StoreView)
                .OrderBy(c => c.Id)
                .Select(c => (c.Hash, c.Manifest))
                .ToList();
        }

        public Task<IReadOnlyList<(UInt160 hash, ContractManifest manifest)>> ListContractsAsync()
            => MakeAsync(ListContracts);

        IReadOnlyList<(ulong requestId, OracleRequest request)> ListOracleRequests()
            => NativeContract.Oracle.GetRequests(neoSystem.StoreView).ToList();

        public Task<IReadOnlyList<(ulong requestId, OracleRequest request)>> ListOracleRequestsAsync()
            => MakeAsync(ListOracleRequests);

        IReadOnlyList<TokenContract> ListTokenContracts()
        {
            using var snapshot = neoSystem.GetSnapshot();
            return snapshot.EnumerateTokenContracts(neoSystem.Settings).ToList();
        }

        public Task<IReadOnlyList<TokenContract>> ListTokenContractsAsync()
            => MakeAsync(ListTokenContracts);

        IReadOnlyList<(string key, string value)> ListStorages(UInt160 scriptHash)
        {
            using var snapshot = neoSystem.GetSnapshot();
            var contract = NativeContract.ContractManagement.GetContract(snapshot, scriptHash);

            if (contract is null)
                return Array.Empty<(string, string)>();

            byte[] prefix = StorageKey.CreateSearchPrefix(contract.Id, default);
            return snapshot.Find(prefix)
                .Select(t => (Convert.ToHexString(t.Key.Key.Span), Convert.ToHexString(t.Value.Value.Span)))
                .ToList();
        }

        public Task<IReadOnlyList<(string key, string value)>> ListStoragesAsync(UInt160 scriptHash)
            => MakeAsync(() => ListStorages(scriptHash));

        public Task<int> PersistContractAsync(ContractState state, IReadOnlyList<(string key, string value)> storagePairs, ContractCommand.OverwriteForce force)
            => MakeAsync(() =>
            {
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Contract download is only supported for single-node consensus");
                }

                return NodeUtility.PersistContract(neoSystem, state, storagePairs, force);
            });

        public Task<int> PersistStorageKeyValueAsync(UInt160 scripthash, (string key, string value) storagePair)
            => MakeAsync(() =>
            {
                var state = NativeContract.ContractManagement.GetContract(neoSystem.StoreView, scripthash);
                if (chain.ConsensusNodes.Count != 1)
                {
                    throw new ArgumentException("Contract storage update is only supported for single-node consensus");
                }

                return NodeUtility.PersistStorageKeyValuePair(neoSystem, state, storagePair, ContractCommand.OverwriteForce.None);
            });

        // warning CS1998: This async method lacks 'await' operators and will run synchronously.
        // EnumerateNotificationsAsync has to be async in order to be polymorphic with OnlineNode's implementation
#pragma warning disable 1998
        public async IAsyncEnumerable<(uint blockIndex, NotificationRecord notification)> EnumerateNotificationsAsync(IReadOnlySet<UInt160>? contractFilter, IReadOnlySet<string>? eventFilter)
        {
            var notifications = persistencePlugin.GetNotifications(SeekDirection.Backward, contractFilter, eventFilter);
            foreach (var (block, _, notification) in notifications)
            {
                yield return (block, notification);
            }
        }
#pragma warning restore 1998

        public Task<bool> IsNep17CompliantAsync(UInt160 contractHash)
        {
            var snapshot = neoSystem.GetSnapshot();
            var validator = new Nep17Token(ProtocolSettings, snapshot, contractHash);

            return Task.FromResult(
                validator.HasValidMethods() &&
                validator.IsSymbolValid() &&
                validator.IsDecimalsValid() &&
                validator.IsBalanceOfValid());
        }

        public Task<bool> IsNep11CompliantAsync(UInt160 contractHash)
        {
            var snapshot = neoSystem.GetSnapshot();
            var validator = new Nep11Token(ProtocolSettings, snapshot, contractHash);

            return Task.FromResult(
                validator.HasValidMethods() &&
                validator.IsSymbolValid() &&
                validator.IsDecimalsValid() &&
                validator.IsBalanceOfValid());
        }
    }
}
