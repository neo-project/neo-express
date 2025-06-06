// Copyright (C) 2015-2024 The Neo Project.
//
// Extensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.Utilities;
using Neo.Extensions;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using static Neo.Utility;
using IReadOnlyStore = Neo.Persistence.IReadOnlyStore<byte[], byte[]>;

namespace NeoTestHarness
{
    using NeoStorage = IReadOnlyDictionary<ReadOnlyMemory<byte>, StorageItem>;

    public static class Extensions
    {
        public static VMState ExecuteScript<T>(this ApplicationEngine engine, params Expression<Action<T>>[] expressions)
            where T : class
        {
            engine.LoadScript<T>(expressions);
            return engine.Execute();
        }

        public static VMState ExecuteScript(this ApplicationEngine engine, Script script)
        {
            engine.LoadScript(script);
            return engine.Execute();
        }

        public static void LoadScript<T>(this ApplicationEngine engine, params Expression<Action<T>>[] expressions)
            where T : class
        {
            var script = engine.SnapshotCache.CreateScript<T>(expressions);
            engine.LoadScript(script);
        }

        public static Script CreateScript<T>(this ApplicationEngine engine, params Expression<Action<T>>[] expressions)
            where T : class
            => CreateScript<T>(engine.SnapshotCache, expressions);

        public static Script CreateScript<T>(this DataCache snapshot, params Expression<Action<T>>[] expressions)
            where T : class
        {
            var scriptHash = snapshot.GetContractScriptHash<T>();
            using var builder = new ScriptBuilder();
            for (int i = 0; i < expressions.Length; i++)
            {
                builder.EmitContractCall(scriptHash, expressions[i]);
            }
            return builder.ToArray();
        }

        public static void EmitContractCall<T>(this ScriptBuilder builder, ApplicationEngine engine, Expression<Action<T>> expression)
            where T : class
            => EmitContractCall<T>(builder, engine.SnapshotCache, expression);

        public static void EmitContractCall<T>(this ScriptBuilder builder, DataCache snapshot, Expression<Action<T>> expression)
            where T : class
        {
            var scriptHash = snapshot.GetContractScriptHash<T>();
            EmitContractCall<T>(builder, scriptHash, expression);
        }

        public static void EmitContractCall<T>(this ScriptBuilder builder, UInt160 scriptHash, Expression<Action<T>> expression)
        {
            var methodCall = (MethodCallExpression)expression.Body;
            var operation = methodCall.Method.Name;

            for (var x = methodCall.Arguments.Count - 1; x >= 0; x--)
            {
                var obj = Expression.Lambda(methodCall.Arguments[x]).Compile().DynamicInvoke();
                var param = ContractParameterParser.ConvertObject(obj);
                builder.EmitPush(param);
            }
            builder.EmitPush(methodCall.Arguments.Count);
            builder.Emit(OpCode.PACK);
            builder.EmitPush(CallFlags.All);
            builder.EmitPush(operation);
            builder.EmitPush(scriptHash);
            builder.EmitSysCall(ApplicationEngine.System_Contract_Call);
        }

        public static NeoStorage GetContractStorages<T>(this ApplicationEngine engine) where T : class
            => GetContractStorages<T>(engine.SnapshotCache);

        public static NeoStorage GetContractStorages<T>(this DataCache snapshot)
            where T : class
        {
            var contract = snapshot.GetContract<T>();
            var prefix = StorageKey.CreateSearchPrefix(contract.Id, default);

            return snapshot.Find(prefix)
                .ToDictionary(s => s.Key.Key, s => s.Value, MemorySequenceComparer.Default);
        }

        public static NeoStorage StorageMap(this NeoStorage storages, byte prefix)
        {
            byte[]? buffer = null;
            try
            {
                buffer = ArrayPool<byte>.Shared.Rent(1);
                buffer[0] = prefix;
                return storages.StorageMap(buffer.AsMemory(0, 1));
            }
            finally
            {
                if (buffer != null)
                    ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static NeoStorage StorageMap(this NeoStorage storages, string prefix)
        {
            byte[]? buffer = null;
            try
            {
                var count = StrictUTF8.GetByteCount(prefix);
                buffer = ArrayPool<byte>.Shared.Rent(count);
                count = StrictUTF8.GetBytes(prefix, buffer);
                return storages.StorageMap(buffer.AsMemory(0, count));
            }
            finally
            {
                if (buffer != null)
                    ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static NeoStorage StorageMap(this NeoStorage storages, ReadOnlyMemory<byte> prefix)
            => storages.Where(kvp => kvp.Key.Span.StartsWith(prefix.Span))
                .ToDictionary(kvp => kvp.Key.Slice(prefix.Length), kvp => kvp.Value, MemorySequenceComparer.Default);


        public static bool TryGetValue(this NeoStorage storages, byte key, [MaybeNullWhen(false)] out StorageItem item)
        {
            byte[]? buffer = null;
            try
            {
                buffer = ArrayPool<byte>.Shared.Rent(1);
                buffer[0] = key;
                return storages.TryGetValue(buffer.AsMemory(0, 1), out item);
            }
            finally
            {
                if (buffer != null)
                    ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static bool TryGetValue(this NeoStorage storages, string key, [MaybeNullWhen(false)] out StorageItem item)
        {
            byte[]? buffer = null;
            try
            {
                var count = StrictUTF8.GetByteCount(key);
                buffer = ArrayPool<byte>.Shared.Rent(count);
                count = StrictUTF8.GetBytes(key, buffer);
                return storages.TryGetValue(buffer.AsMemory(0, count), out item);
            }
            finally
            {
                if (buffer != null)
                    ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static bool TryGetValue(this NeoStorage storage, UInt160 key, [MaybeNullWhen(false)] out StorageItem item)
            => storage.TryGetValue(key.ToArray(), out item);

        public static bool TryGetValue(this NeoStorage storage, UInt256 key, [MaybeNullWhen(false)] out StorageItem item)
            => storage.TryGetValue(key.ToArray(), out item);

        public static UInt160 GetContractScriptHash<T>(this ApplicationEngine engine)
            where T : class
            => GetContractScriptHash<T>(engine.SnapshotCache);

        public static UInt160 GetContractScriptHash<T>(this DataCache snapshot)
            where T : class
            => snapshot.GetContract<T>().Hash;

        public static ContractState GetContract<T>(this ApplicationEngine engine)
            where T : class
            => GetContract<T>(engine.SnapshotCache);

        public static ContractState GetContract<T>(this DataCache snapshot)
            where T : class
        {
            var contractName = GetContractName(typeof(T));
            return snapshot.GetContract(contractName);

            static string GetContractName(Type type)
            {
                if (type.IsNested)
                {
                    return GetContractName(type.DeclaringType ?? throw new Exception("reflection IsNested DeclaringType returned null"));
                }

                var contractAttrib = Attribute.GetCustomAttribute(type, typeof(ContractAttribute)) as ContractAttribute;
                if (contractAttrib != null)
                    return contractAttrib.Name;

                var descriptionAttrib = Attribute.GetCustomAttribute(type, typeof(DescriptionAttribute)) as DescriptionAttribute;
                if (descriptionAttrib != null)
                    return descriptionAttrib.Description;

                throw new Exception("reflection - FullName returned null");
            }
        }

        public static ContractState GetContract(this ApplicationEngine engine, string contractName)
            => GetContract(engine.SnapshotCache, contractName);

        public static ContractState GetContract(this DataCache snapshot, string contractName)
        {
            foreach (var contractState in NativeContract.ContractManagement.ListContracts(snapshot))
            {
                var name = contractState.Id >= 0 ? contractState.Manifest.Name : "Neo.SmartContract.Native." + contractState.Manifest.Name;
                if (string.Equals(contractName, name))
                {
                    return contractState;
                }
            }

            throw new Exception($"couldn't find {contractName} contract");
        }

        public static StoreCache GetSnapshot(this CheckpointFixture fixture)
        {
            // Follow the same pattern as runner: ICheckpointStore -> MemoryTrackingStore -> StoreCache
            // CheckpointStore implements both IReadOnlyStore<StorageKey, StorageItem> and IReadOnlyStore<byte[], byte[]>
            var byteStore = (IReadOnlyStore)fixture.CheckpointStore;
            var store = new MemoryTrackingStore(byteStore);
            return new StoreCache(store.GetSnapshot());
        }
    }
}
