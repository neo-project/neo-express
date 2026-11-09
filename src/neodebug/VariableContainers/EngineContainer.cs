// Copyright (C) 2015-2026 The Neo Project.
//
// EngineContainer.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace NeoDebug.Neo3
{
    /// <summary>Exposes engine-wide state (such as gas consumed) as a debug scope.</summary>
    internal class EngineContainer : IVariableContainer
    {
        private readonly IApplicationEngine _engine;

        public EngineContainer(IApplicationEngine engine)
        {
            _engine = engine;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            yield return new Variable
            {
                Name = nameof(IApplicationEngine.GasConsumed),
                Value = _engine.GasConsumed.AsBigDecimal().ToString(),
            };
        }
    }
}
