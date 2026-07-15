// Copyright (C) 2015-2026 The Neo Project.
//
// NeoMapContainer.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoMap = Neo.VM.Types.Map;

namespace NeoDebug.Neo3
{
    /// <summary>Expands a NeoVM map into one child variable per entry, keyed by the rendered map key.</summary>
    internal class NeoMapContainer : IVariableContainer
    {
        private readonly NeoMap _map;

        public NeoMapContainer(NeoMap map)
        {
            _map = map;
        }

        public static Variable Create(IVariableManager manager, NeoMap map, string name)
        {
            var container = new NeoMapContainer(map);
            return new Variable()
            {
                Name = name,
                Value = $"Map[{map.Count}]",
                VariablesReference = manager.Add(container),
                NamedVariables = map.Count,
            };
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            foreach (var key in _map.Keys)
            {
                // A map key becomes the child name; keep its scalar value instead of an expandable ByteString label.
                yield return _map[key].ToVariable(manager, key.ToMapKeyString());
            }
        }
    }
}
