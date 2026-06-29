// Copyright (C) 2015-2026 The Neo Project.
//
// StorageContainerBase.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.SmartContract;
using System.Globalization;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// Renders a contract's storage as debug variables (one entry per key, each expandable into its key and
    /// value bytes) and resolves <c>#storage[hash].key|item</c> evaluate expressions. Subclasses supply the
    /// storage rows for a given backend.
    /// </summary>
    internal abstract class StorageContainerBase : IVariableContainer
    {
        protected abstract IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> GetStorages();

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            foreach (var (key, item) in GetStorages())
            {
                var keyHashCode = key.Span.GetSequenceHashCode().ToString("x8");
                var kvp = new KvpContainer(key, item, keyHashCode);
                yield return new Variable()
                {
                    Name = keyHashCode,
                    Value = string.Empty,
                    VariablesReference = manager.Add(kvp),
                    NamedVariables = 2,
                };
            }
        }

        public (StackItem? item, ReadOnlyMemory<char> remaining) Evaluate(ReadOnlyMemory<char> expression)
        {
            if (TryGetKeyHash(expression, out var keyHash)
                && TryFindStorage(GetStorages(), keyHash, out var storage))
            {
                var remain = expression.Slice(19);
                if (remain.Length >= 3 && remain.Span.Slice(0, 3).SequenceEqual("key"))
                {
                    return (storage.key, remain.Slice(3));
                }
                else if (remain.Length >= 4 && remain.Span.Slice(0, 4).SequenceEqual("item"))
                {
                    return (storage.item.Value, remain.Slice(4));
                }
            }

            throw new InvalidOperationException("Invalid storage evaluation");

            static bool TryGetKeyHash(ReadOnlyMemory<char> expression, out int value)
            {
                if (expression.Length >= 19
                    && expression.StartsWith(DebugSession.STORAGE_PREFIX)
                    && expression.Span[8] == '['
                    && expression.Span[17] == ']'
                    && expression.Span[18] == '.'
                    && int.TryParse(expression.Slice(9, 8).Span, NumberStyles.HexNumber, null, out value))
                {
                    return true;
                }

                value = default;
                return false;
            }

            static bool TryFindStorage(IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages, int hashCode, out (ReadOnlyMemory<byte> key, StorageItem item) storage)
            {
                foreach (var (key, item) in storages)
                {
                    if (hashCode == key.Span.GetSequenceHashCode())
                    {
                        storage = (key, item);
                        return true;
                    }
                }

                storage = default;
                return false;
            }
        }

        private class KvpContainer : IVariableContainer
        {
            private readonly ReadOnlyMemory<byte> _key;
            private readonly StorageItem _item;
            private readonly string _prefix;

            public KvpContainer(ReadOnlyMemory<byte> key, StorageItem item, string hashCode)
            {
                _key = key;
                _item = item;
                _prefix = $"{DebugSession.STORAGE_PREFIX}[{hashCode}].";
            }

            public IEnumerable<Variable> Enumerate(IVariableManager manager)
            {
                var keyItem = ByteArrayContainer.Create(manager, _key, "key");
                keyItem.EvaluateName = _prefix + "key";
                yield return keyItem;

                var valueItem = ByteArrayContainer.Create(manager, _item.Value, "item");
                valueItem.EvaluateName = _prefix + "item";
                yield return valueItem;
            }
        }
    }

    /// <summary>A <see cref="StorageContainerBase"/> over an already-materialized set of storage rows.</summary>
    internal sealed class StorageContainer : StorageContainerBase
    {
        private readonly IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> _storages;

        public StorageContainer(IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages)
        {
            _storages = storages;
        }

        protected override IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> GetStorages() => _storages;
    }
}
