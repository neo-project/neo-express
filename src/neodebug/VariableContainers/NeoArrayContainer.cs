// Copyright (C) 2015-2026 The Neo Project.
//
// NeoArrayContainer.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoArray = Neo.VM.Types.Array;
using NeoStruct = Neo.VM.Types.Struct;

namespace NeoDebug.Neo3
{
    /// <summary>Expands a NeoVM array or struct into its indexed child elements.</summary>
    internal class NeoArrayContainer : IVariableContainer
    {
        private readonly NeoArray _array;

        public NeoArrayContainer(NeoArray array)
        {
            _array = array;
        }

        public static Variable Create(IVariableManager manager, NeoArray array, string name)
        {
            var typeName = array is NeoStruct ? "Struct" : "Array";
            var container = new NeoArrayContainer(array);
            return new Variable()
            {
                Name = name,
                Value = $"{typeName}[{array.Count}]",
                VariablesReference = manager.Add(container),
                IndexedVariables = array.Count,
            };
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < _array.Count; i++)
            {
                yield return _array[i].ToVariable(manager, $"{i}");
            }
        }
    }
}
