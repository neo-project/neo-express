// Copyright (C) 2015-2026 The Neo Project.
//
// SlotContainer.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System.Collections.Immutable;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    /// <summary>Renders a raw VM slot (evaluation stack, arguments, locals, statics, result stack) as indexed variables.</summary>
    internal class SlotContainer : IVariableContainer
    {
        private readonly IReadOnlyList<StackItem> _slot;
        private readonly string _prefix;

        public SlotContainer(string prefix, IReadOnlyList<StackItem>? slot)
        {
            _slot = slot ?? ImmutableList<StackItem>.Empty;
            _prefix = prefix;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            for (int i = 0; i < _slot.Count; i++)
            {
                var variable = _slot[i].ToVariable(manager, $"{_prefix}{i}");
                variable.EvaluateName = variable.Name;
                yield return variable;
            }
        }
    }
}
