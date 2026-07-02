// Copyright (C) 2015-2026 The Neo Project.
//
// VariableManager.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Diagnostics.CodeAnalysis;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// Tracks the variable containers referenced in the current stop and assigns each a stable, non-zero
    /// <c>variablesReference</c> handle. The table is cleared each time execution stops.
    /// </summary>
    public class VariableManager : IVariableManager
    {
        private readonly Dictionary<int, IVariableContainer> _containers = new();
        private int _nextId = 1;

        public void Clear()
        {
            _containers.Clear();
            _nextId = 1;
        }

        public bool TryGet(int id, [MaybeNullWhen(false)] out IVariableContainer container)
            => _containers.TryGetValue(id, out container);

        // A monotonically increasing id avoids the hash-collision failure of keying on GetHashCode, and
        // keeps every handle non-zero (a variablesReference of 0 means "no children" in the protocol).
        public int Add(IVariableContainer container)
        {
            var id = _nextId++;
            _containers.Add(id, container);
            return id;
        }
    }
}
