// Copyright (C) 2015-2026 The Neo Project.
//
// DebugApplicationEngine.InvocationStackAdapter.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections;

namespace NeoDebug.Neo3
{
    internal partial class DebugApplicationEngine
    {
        /// <summary>Projects the live VM invocation stack as a collection of <see cref="IExecutionContext"/>.</summary>
        private class InvocationStackAdapter : IReadOnlyCollection<IExecutionContext>
        {
            private readonly DebugApplicationEngine _engine;

            public InvocationStackAdapter(DebugApplicationEngine engine)
            {
                _engine = engine;
            }

            public int Count => _engine.InvocationStack.Count;

            public IEnumerator<IExecutionContext> GetEnumerator()
            {
                foreach (var context in _engine.InvocationStack)
                {
                    yield return new ExecutionContextAdapter(context, _engine._scriptIdMap);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
