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
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// Renders a contract's storage as debug variables (one entry per key, each expandable into its key and
    /// value bytes) and resolves <c>#storage[key-hex].key|item</c> evaluate expressions. Subclasses supply the
    /// storage rows for a given backend.
    /// </summary>
    internal abstract class StorageContainerBase : IVariableContainer
    {
        protected abstract IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> GetStorages();

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            foreach (var (key, item) in GetStorages())
            {
                var keyIdentifier = Convert.ToHexString(key.Span).ToLowerInvariant();
                var kvp = new KvpContainer(key, item, keyIdentifier);
                yield return new Variable()
                {
                    Name = keyIdentifier,
                    Value = string.Empty,
                    VariablesReference = manager.Add(kvp),
                    NamedVariables = 2,
                };
            }
        }

        public (StackItem? item, ReadOnlyMemory<char> remaining) Evaluate(ReadOnlyMemory<char> expression)
        {
            if (TryGetKeyIdentifier(expression, out var keyIdentifier, out var remainingOffset)
                && TryFindStorage(GetStorages(), keyIdentifier.Span, out var storage))
            {
                var remain = expression.Slice(remainingOffset);
                if (remain.Length >= 3 && remain.Span.Slice(0, 3).SequenceEqual("key"))
                {
                    return (storage.key, remain.Slice(3));
                }
                else if (remain.Length >= 4 && remain.Span.Slice(0, 4).SequenceEqual("item"))
                {
                    return (storage.item.Value, remain.Slice(4));
                }
            }

            return (null, expression);

            static bool TryGetKeyIdentifier(ReadOnlyMemory<char> expression,
                out ReadOnlyMemory<char> keyIdentifier, out int remainingOffset)
            {
                var prefixLength = DebugSession.STORAGE_PREFIX.Length;
                var keyStart = prefixLength + 1;
                if (expression.Length >= keyStart + 2
                    && expression.StartsWith(DebugSession.STORAGE_PREFIX)
                    && expression.Span[prefixLength] == '[')
                {
                    var closingOffset = expression.Span[keyStart..].IndexOf(']');
                    if (closingOffset >= 0)
                    {
                        var closingIndex = keyStart + closingOffset;
                        var candidate = expression.Slice(keyStart, closingOffset);
                        if (closingIndex + 1 < expression.Length
                            && expression.Span[closingIndex + 1] == '.'
                            && candidate.Length % 2 == 0
                            && IsHexIdentifier(candidate.Span))
                        {
                            keyIdentifier = candidate;
                            remainingOffset = closingIndex + 2;
                            return true;
                        }
                    }
                }

                keyIdentifier = default;
                remainingOffset = default;
                return false;
            }

            static bool IsHexIdentifier(ReadOnlySpan<char> value)
            {
                foreach (var character in value)
                {
                    if (!char.IsAsciiHexDigit(character))
                        return false;
                }

                return true;
            }

            static bool TryFindStorage(IEnumerable<(ReadOnlyMemory<byte> key, StorageItem item)> storages,
                ReadOnlySpan<char> keyIdentifier, out (ReadOnlyMemory<byte> key, StorageItem item) storage)
            {
                foreach (var (key, item) in storages)
                {
                    if (keyIdentifier.Equals(Convert.ToHexString(key.Span), StringComparison.OrdinalIgnoreCase))
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
