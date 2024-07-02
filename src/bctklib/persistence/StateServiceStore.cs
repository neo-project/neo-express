// Copyright (C) 2015-2024 The Neo Project.
//
// StateServiceStore.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Utilities;
using Neo.IO;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using OneOf;
using OneOf.Types;
using RocksDbSharp;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;

namespace Neo.BlockchainToolkit.Persistence
{
    public sealed partial class StateServiceStore : IReadOnlyStore, IDisposable
    {
        internal interface ICacheClient : IDisposable
        {
            bool TryGetCachedStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, out byte[]? value);
            void CacheStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, byte[]? value);
            bool TryGetCachedFoundStates(UInt160 contractHash, byte? prefix, out IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> value);
            void DropCachedFoundStates(UInt160 contractHash, byte? prefix);
            ICacheSnapshot GetFoundStatesSnapshot(UInt160 contractHash, byte? prefix);
        }

        internal interface ICacheSnapshot : IDisposable
        {
            void Add(ReadOnlyMemory<byte> key, byte[] value);
            void Commit();
        }

        public const string LoggerCategory = "Neo.BlockchainToolkit.Persistence.StateServiceStore";
        readonly static DiagnosticSource logger = new DiagnosticListener(LoggerCategory);

        const byte ContractMgmt_Prefix_Contract = 8;
        const byte ContractMgmt_Prefix_ContractHash = 12;
        const byte Ledger_Prefix_BlockHash = 9;
        const byte Ledger_Prefix_CurrentBlock = 12;
        const byte Ledger_Prefix_Block = 5;
        const byte Ledger_Prefix_Transaction = 11;
        const byte NEO_Prefix_Candidate = 33;
        const byte NEO_Prefix_GasPerBlock = 29;
        const byte NEO_Prefix_VoterRewardPerCommittee = 23;
        const byte Oracle_Prefix_Request = 7;
        const byte RoleMgmt_Prefix_NeoFSAlphabetNode = (byte)Role.NeoFSAlphabetNode;
        const byte RoleMgmt_Prefix_Oracle = (byte)Role.Oracle;
        const byte RoleMgmt_Prefix_StateValidator = (byte)Role.StateValidator;

        static readonly IReadOnlyDictionary<int, IReadOnlyList<byte>> contractSeekMap = new Dictionary<int, IReadOnlyList<byte>>()
        {
            { NativeContract.ContractManagement.Id, new [] { ContractMgmt_Prefix_Contract, ContractMgmt_Prefix_ContractHash } },
            { NativeContract.NEO.Id, new [] { NEO_Prefix_Candidate, NEO_Prefix_GasPerBlock } },
            { NativeContract.Oracle.Id, new [] { Oracle_Prefix_Request } },
            { NativeContract.RoleManagement.Id, new [] { RoleMgmt_Prefix_NeoFSAlphabetNode, RoleMgmt_Prefix_Oracle, RoleMgmt_Prefix_StateValidator } }
        };

        readonly RpcClient rpcClient;
        readonly ICacheClient cacheClient;
        readonly BranchInfo branchInfo;
        readonly IReadOnlyDictionary<int, UInt160> contractMap;
        readonly IReadOnlyDictionary<UInt160, string> contractNameMap;
        bool disposed = false;

        public ProtocolSettings Settings => branchInfo.ProtocolSettings;

        public StateServiceStore(string uri, in BranchInfo branchInfo)
            : this(new Uri(uri), branchInfo) { }

        public StateServiceStore(Uri uri, in BranchInfo branchInfo)
            : this(new RpcClient(uri), branchInfo) { }

        public StateServiceStore(RpcClient rpcClient, in BranchInfo branchInfo)
            : this(rpcClient, new MemoryCacheClient(), branchInfo) { }

        public StateServiceStore(string uri, in BranchInfo branchInfo, RocksDb db, bool shared = false, string? familyNamePrefix = null)
            : this(new Uri(uri), branchInfo, db, shared, familyNamePrefix) { }

        public StateServiceStore(Uri uri, in BranchInfo branchInfo, RocksDb db, bool shared = false, string? familyNamePrefix = null)
            : this(new RpcClient(uri), branchInfo, db, shared, familyNamePrefix) { }

        public StateServiceStore(RpcClient rpcClient, in BranchInfo branchInfo, RocksDb db, bool shared = false, string? familyNamePrefix = null)
            : this(rpcClient, new RocksDbCacheClient(db, shared, familyNamePrefix ?? nameof(StateServiceStore)), branchInfo) { }

        internal StateServiceStore(RpcClient rpcClient, ICacheClient cacheClient, in BranchInfo branchInfo)
        {
            this.rpcClient = rpcClient;
            this.cacheClient = cacheClient;
            this.branchInfo = branchInfo;
            contractMap = branchInfo.Contracts.ToDictionary(c => c.Id, c => c.Hash);
            contractNameMap = branchInfo.Contracts.ToDictionary(c => c.Hash, c => c.Name);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                rpcClient.Dispose();
                cacheClient.Dispose();
                disposed = true;
            }
        }

        public async Task<OneOf<Success, Error<string>>> PrefetchAsync(UInt160 contractHash, CancellationToken token, Action<RpcFoundStates>? callback = null)
        {
            var info = branchInfo.Contracts.Single(c => c.Hash == contractHash);

            if (info.Id < 0)
            {
                // if prefetch of a native contract was requested, prefetch each prefix listed in contractSeekMap
                if (contractSeekMap.TryGetValue(info.Id, out var prefixes))
                {
                    var anyPrefixDownloaded = false;

                    for (int i = 0; i < prefixes.Count; i++)
                    {
                        if (!cacheClient.TryGetCachedFoundStates(contractHash, prefixes[i], out var _))
                        {
                            anyPrefixDownloaded = true;
                            await DownloadStatesAsync(contractHash, prefixes[i], callback, token).ConfigureAwait(false);
                        }
                    }

                    return anyPrefixDownloaded
                        ? default(Success)
                        : new Error<string>($"{info.Name} contract ({contractHash}) already fetched");
                }
                else
                {
                    return new Error<string>($"Prefetch is not supported for ${info.Name} native contract");
                }
            }
            else
            {
                if (cacheClient.TryGetCachedFoundStates(contractHash, null, out var _))
                {
                    return new Error<string>($"{info.Name} contract ({contractHash}) already fetched");
                }
                else
                {
                    await DownloadStatesAsync(contractHash, null, callback, token).ConfigureAwait(false);
                    return default(Success);
                }
            }
        }

        public static async Task<BranchInfo> GetBranchInfoAsync(RpcClient rpcClient, uint index)
        {
            var versionTask = rpcClient.GetVersionAsync();
            var blockHashTask = rpcClient.GetBlockHashAsync(index)
                .ContinueWith(task => UInt256.Parse(task.Result),
                              cancellationToken: default,
                              TaskContinuationOptions.OnlyOnRanToCompletion,
                              TaskScheduler.Default);
            var stateRoot = await rpcClient.GetStateRootAsync(index).ConfigureAwait(false);
            var contractsTask = GetContractsAsync(rpcClient, stateRoot.RootHash);

            await Task.WhenAll(versionTask, blockHashTask, contractsTask).ConfigureAwait(false);

            var version = await versionTask.ConfigureAwait(false);
            var blockHash = await blockHashTask.ConfigureAwait(false);
            var contracts = await contractsTask.ConfigureAwait(false);

            return new BranchInfo(
                version.Protocol.Network,
                version.Protocol.AddressVersion,
                index,
                blockHash,
                stateRoot.RootHash,
                contracts);

            static async Task<IReadOnlyList<ContractInfo>> GetContractsAsync(RpcClient rpcClient, UInt256 rootHash)
            {
                const byte ContractManagement_Prefix_Contract = 8;

                using var memoryOwner = MemoryPool<byte>.Shared.Rent(1);
                memoryOwner.Memory.Span[0] = ContractManagement_Prefix_Contract;
                var prefix = memoryOwner.Memory[..1];

                var contracts = new List<ContractInfo>();
                var from = Array.Empty<byte>();
                while (true)
                {
                    var found = await rpcClient.FindStatesAsync(rootHash, NativeContract.ContractManagement.Hash, prefix, from.AsMemory()).ConfigureAwait(false);
                    ValidateFoundStates(rootHash, found);
                    for (int i = 0; i < found.Results.Length; i++)
                    {
                        var (key, value) = found.Results[i];
                        if (key.AsSpan().StartsWith(prefix.Span))
                        {
                            // Temporary fix --> https://github.com/neo-project/neo/issues/2829
                            try
                            {
                                var state = new StorageItem(value).GetInteroperable<ContractState>();
                                contracts.Add(new ContractInfo(state.Id, state.Hash, state.Manifest.Name));
                            }
                            catch
                            {

                            }
                        }
                    }
                    if (!found.Truncated || found.Results.Length == 0)
                        break;
                    from = found.Results[^1].key;
                }
                return contracts;
            }
        }

        static void ValidateFoundStates(UInt256 rootHash, RpcFoundStates foundStates)
        {
            if (foundStates.Results.Length > 0)
            {
                ValidateProof(rootHash, foundStates.FirstProof, foundStates.Results[0]);
            }
            if (foundStates.Results.Length > 1)
            {
                ValidateProof(rootHash, foundStates.LastProof, foundStates.Results[^1]);
            }

            static void ValidateProof(UInt256 rootHash, byte[]? proof, (byte[] key, byte[] value) result)
            {
                var (storageKey, storageValue) = Utility.VerifyProof(rootHash, proof);
                if (!result.key.AsSpan().SequenceEqual(storageKey.Key.Span))
                    throw new Exception("Incorrect StorageKey");
                if (!result.value.AsSpan().SequenceEqual(storageValue))
                    throw new Exception("Incorrect StorageItem");
            }
        }

        public byte[]? TryGet(byte[]? _key)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StateServiceStore));

            _key ??= Array.Empty<byte>();
            var contractId = BinaryPrimitives.ReadInt32LittleEndian(_key.AsSpan(0, 4));
            var key = _key.AsMemory(4);

            if (contractId == NativeContract.Ledger.Id)
            {
                // Since blocks and transactions are immutable and already available via other
                // JSON-RPC methods, the state service does not store ledger contract data.
                // StateServiceStore needs to translate LedgerContract calls into equivalent
                // non-state service calls.

                // LedgerContract stores the current block's hash and index in a single storage
                // recorded keyed by Ledger_Prefix_CurrentBlock. StateServiceStore is initialized
                // with the branching index, so TryGet needs the associated block hash for this index
                // in order to correctly construct the serialized storage
                var prefix = key.Span[0];
                if (prefix == Ledger_Prefix_CurrentBlock)
                {
                    Debug.Assert(key.Length == 1);

                    var @struct = new VM.Types.Struct() { branchInfo.IndexHash.ToArray(), branchInfo.Index };
                    return BinarySerializer.Serialize(@struct, ExecutionEngineLimits.Default with { MaxItemSize = 1024 * 1024 });
                }

                // all other ledger contract prefixes (Block, BlockHash and Transaction) store immutable
                // data, so this data can be directly retrieved from LedgerContract storage
                if (prefix == Ledger_Prefix_Block
                    || prefix == Ledger_Prefix_BlockHash
                    || prefix == Ledger_Prefix_Transaction)
                {
                    return GetStorage(NativeContract.Ledger.Hash, key,
                        () => rpcClient.GetStorage(NativeContract.Ledger.Hash, key.Span));
                }

                throw new NotSupportedException(
                    $"{nameof(StateServiceStore)} does not support TryGet method for {nameof(LedgerContract)} with {Convert.ToHexString(key.Span)} key");
            }

            if (contractId == NativeContract.NEO.Id
                && key.Span[0] == NEO_Prefix_VoterRewardPerCommittee)
            {
                // as of Neo 3.4, the NeoToken contract only seeks over VoterRewardPerCommittee data.
                // This exception will never be triggered unless a future NeoToken contract update uses does a keyed read
                // for a record with this prefix 
                throw new NotSupportedException(
                    $"{nameof(StateServiceStore)} does not support TryGet method for {nameof(NeoToken)} with {Convert.ToHexString(key.Span)} key");
            }

            if (!contractMap.TryGetValue(contractId, out var contractHash))
            {
                // if contractId isn't in contractMap, the state service has no record of it at the
                // branch index height. Return null directly.
                return null;
            }

            if (contractId < 0)
            {
                // contractSeekMap contains info on which native contract ids / prefixes can use seek methods
                // For prefixes that need seek capability, ensure all records with that prefix are cached locally
                // then retrieve the specifically keyed value from cache.
                if (contractSeekMap.TryGetValue(contractId, out var prefixes))
                {
                    var prefix = key.Span[0];
                    if (prefixes.Contains(prefix))
                    {
                        return GetFromStates(contractHash, prefix, key);
                    }
                }

                // otherwise, retrieve and cache the keyed value 
                return GetStorage(contractHash, key,
                    () => rpcClient.GetProvenState(branchInfo.RootHash, contractHash, key.Span));
            }

            // since there is no way to know the data usage patterns for deployed contracts download
            // and cache all records for that contract then retrieve the keyed value from cache
            return GetFromStates(contractHash, null, key);

            byte[]? GetFromStates(UInt160 contractHash, byte? prefix, ReadOnlyMemory<byte> key)
            {
                return FindStates(contractHash, prefix)
                    .FirstOrDefault(kvp => MemorySequenceComparer.Equals(kvp.key.Span, key.Span)).value;
            }
        }

        byte[]? GetStorage(UInt160 contractHash, ReadOnlyMemory<byte> key, Func<byte[]?> getStorageFromService)
        {
            if (cacheClient.TryGetCachedStorage(contractHash, key, out var value))
                return value;

            const string loggerName = nameof(GetStorage);
            Activity? activity = null;
            if (logger.IsEnabled(loggerName))
            {
                activity = new Activity(loggerName);
                logger.StartActivity(activity, new GetStorageStart(contractHash, contractNameMap[contractHash], key));
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = getStorageFromService();
                cacheClient.CacheStorage(contractHash, key, result);
                return result;
            }
            catch (RpcException ex) when (ex.HResult == -100)
            {
                // if the getstorage method throws an RPC Exception w/ HResult == -100, it means the 
                // storage key could not be found. At the storage layer, this means returning a null byte arrray. 
                return null;
            }
            finally
            {
                stopwatch.Stop();
                if (activity is not null)
                    logger.StopActivity(activity, new GetStorageStop(stopwatch.Elapsed));
            }
        }

        public bool Contains(byte[]? key) => TryGet(key) is not null;

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[]? _key, SeekDirection direction)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StateServiceStore));

            _key ??= Array.Empty<byte>();
            var contractId = BinaryPrimitives.ReadInt32LittleEndian(_key.AsSpan(0, 4));
            var key = _key.AsMemory(4);

            if (contractId == NativeContract.Ledger.Id)
            {
                // Because the state service does not store ledger contract data, the seek method cannot
                // be implemented for the ledger contract. As of Neo 3.4, the Ledger contract only 
                // uses Seek in the Initialized method to check for the existence of any value with a
                // Prefix_Block prefix. In order to support this single scenario, return a single empty
                // byte array enumerable. This will enable .Any() LINQ method to return true, but will
                // fail if the caller attempts to deserialize the provided array into a trimmed block.

                var prefix = key.Span[0];
                if (prefix == Ledger_Prefix_Block)
                {
                    Debug.Assert(_key.Length == 5);
                    return Enumerable.Repeat((_key, Array.Empty<byte>()), 1);
                }

                throw new NotSupportedException($"{nameof(StateServiceStore)} does not support Seek method for {nameof(LedgerContract)} with {prefix} prefix");
            }

            if (contractId == NativeContract.NEO.Id
                && key.Span[0] == NEO_Prefix_VoterRewardPerCommittee)
            {
                // For committee members, a new VoterRewardPerCommittee record is created every epoch
                // (21 blocks / 5 minutes). Since the number of committee members == the number of 
                // blocks in an epoch, this averages one record per block. Given that mainnet is 2.2 
                // million blocks as of Sept 2022, downloading all these records is not feasible.

                // VoterRewardPerCommittee records are used to determine GAS token rewards for committee 
                // members. Since GAS reward calculation for committee members is not a relevant scenario
                // for Neo contract developers, StateServiceStore simply returns an empty array

                return Array.Empty<(byte[], byte[])>();
            }

            if (!contractMap.TryGetValue(contractId, out var contractHash))
            {
                // if contractId isn't in contractMap, the state service has no record of it at the
                // branch index height. Return empty enumerable directly.
                return Enumerable.Empty<(byte[] Key, byte[] Value)>();
            }

            if (contractId < 0)
            {
                var prefix = key.Span[0];
                if (contractSeekMap.TryGetValue(contractId, out var prefixes))
                {
                    if (prefixes.Contains(prefix))
                    {
                        var states = FindStates(contractHash, prefix);
                        return ConvertStates(key, states);
                    }
                    else
                    {
                        var contractName = contractNameMap[contractHash];
                        throw new NotSupportedException(
                            $"{nameof(StateServiceStore)} does not support Seek method for {contractName} with {prefix} prefix");
                    }
                }
                else
                {
                    var contractName = contractNameMap[contractHash];
                    throw new NotSupportedException(
                        $"{nameof(StateServiceStore)} does not support Seek method for {contractName}");
                }
            }

            {
                var states = FindStates(contractHash, null);
                return ConvertStates(key, states);
            }

            IEnumerable<(byte[] Key, byte[] Value)> ConvertStates(ReadOnlyMemory<byte> key, IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> states)
            {
                var comparer = direction == SeekDirection.Forward
                   ? MemorySequenceComparer.Default
                   : MemorySequenceComparer.Reverse;

                return states
                    .Where(kvp => key.Length == 0 || comparer.Compare(kvp.key, key) >= 0)
                    .Select(kvp =>
                    {
                        var k = new byte[kvp.key.Length + 4];
                        BinaryPrimitives.WriteInt32LittleEndian(k.AsSpan(0, 4), contractId);
                        kvp.key.CopyTo(k.AsMemory(4));
                        return (key: k, kvp.value);
                    })
                    .OrderBy(kvp => kvp.key, comparer);
            }
        }

        IEnumerable<(ReadOnlyMemory<byte> key, byte[] value)> FindStates(UInt160 contractHash, byte? prefix)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(StateServiceStore));

            if (cacheClient.TryGetCachedFoundStates(contractHash, prefix, out var values))
            {
                return values;
            }

            var count = DownloadStates(contractHash, prefix, CancellationToken.None);

            if (cacheClient.TryGetCachedFoundStates(contractHash, prefix, out values))
            {
                return values;
            }

            throw new Exception("DownloadStates failed");
        }

        async Task<int> DownloadStatesAsync(UInt160 contractHash, byte? prefix, Action<RpcFoundStates>? callback, CancellationToken token)
        {
            const string loggerName = nameof(DownloadStatesAsync);
            Activity? activity = DownloadStatesStart(loggerName, contractHash, prefix);
            var count = 0;
            var stopwatch = Stopwatch.StartNew();
            using var prefixOwner = GetPrefixOwner(prefix);
            try
            {
                var from = Array.Empty<byte>();
                using var snapshot = cacheClient.GetFoundStatesSnapshot(contractHash, prefix);
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var found = await rpcClient.FindStatesAsync(branchInfo.RootHash, contractHash, prefixOwner.Memory, from).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    callback?.Invoke(found);
                    if (WriteFoundStates(found, snapshot, out from, ref count, loggerName))
                        break;
                }
                snapshot.Commit();
                return count;
            }
            catch
            {
                cacheClient.DropCachedFoundStates(contractHash, prefix);
                throw;
            }
            finally
            {
                DownloadStatesStop(activity, count, stopwatch);
            }
        }

        int DownloadStates(UInt160 contractHash, byte? prefix, CancellationToken token)
        {
            const string loggerName = nameof(DownloadStates);
            Activity? activity = DownloadStatesStart(loggerName, contractHash, prefix);
            var count = 0;
            var stopwatch = Stopwatch.StartNew();
            using var prefixOwner = GetPrefixOwner(prefix);
            try
            {
                var from = Array.Empty<byte>();
                using var snapshot = cacheClient.GetFoundStatesSnapshot(contractHash, prefix);
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var found = rpcClient.FindStates(branchInfo.RootHash, contractHash, prefixOwner.Memory.Span, from);
                    token.ThrowIfCancellationRequested();
                    if (WriteFoundStates(found, snapshot, out from, ref count, loggerName))
                        break;
                }
                snapshot.Commit();
                return count;
            }
            catch
            {
                cacheClient.DropCachedFoundStates(contractHash, prefix);
                throw;
            }
            finally
            {
                DownloadStatesStop(activity, count, stopwatch);
            }
        }

        Activity? DownloadStatesStart(string loggerName, UInt160 contractHash, byte? prefix)
        {
            if (logger.IsEnabled(loggerName))
            {
                var activity = new Activity(loggerName);
                logger.StartActivity(activity, new DownloadStatesStart(contractHash, contractNameMap[contractHash], prefix));
                return activity;
            }
            return null;
        }

        static void DownloadStatesStop(Activity? activity, int count, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            if (activity is not null)
            {
                logger.StopActivity(activity, new DownloadStatesStop(count, stopwatch.Elapsed));
            }
        }

        static IMemoryOwner<byte> GetPrefixOwner(byte? prefix)
        {
            if (!prefix.HasValue)
                return NullMemoryOwner<byte>.Instance;
            var owner = ExactMemoryOwner<byte>.Rent(1);
            owner.Memory.Span[0] = prefix.Value;
            return owner;
        }

        bool WriteFoundStates(RpcFoundStates found, ICacheSnapshot snapshot, out byte[] from, ref int count, string loggerName)
        {
            ValidateFoundStates(branchInfo.RootHash, found);
            count += found.Results.Length;
            if (logger.IsEnabled(loggerName) && found.Truncated)
            {
                logger.Write($"{loggerName}.Found", new DownloadStatesFound(count, found.Results.Length));
            }
            for (int i = 0; i < found.Results.Length; i++)
            {
                var (key, value) = found.Results[i];
                snapshot.Add(key, value);
            }

            if (!found.Truncated || found.Results.Length == 0)
            {
                from = Array.Empty<byte>();
                return true;
            }

            from = found.Results[^1].key;
            return false;
        }
    }
}
