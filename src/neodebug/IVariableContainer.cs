// Copyright (C) 2015-2026 The Neo Project.
//
// IVariableContainer.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace NeoDebug.Neo3
{
    /// <summary>A node in the debug variable tree that can lazily expand into child <see cref="Variable"/>s.</summary>
    public interface IVariableContainer
    {
        IEnumerable<Variable> Enumerate(IVariableManager manager);
    }
}
