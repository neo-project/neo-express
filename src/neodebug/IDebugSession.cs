// Copyright (C) 2015-2026 The Neo Project.
//
// IDebugSession.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Thread = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Thread;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// The debug-engine-facing contract the <see cref="DebugAdapter"/> drives. One implementation backs both
    /// the trace-replay and live engines; the adapter never references a concrete engine.
    /// </summary>
    public interface IDebugSession
    {
        EvaluateResponse Evaluate(EvaluateArguments args);
        string GetExceptionInfo();
        IEnumerable<Scope> GetScopes(ScopesArguments args);
        SourceResponse GetSource(SourceArguments arguments);
        IEnumerable<StackFrame> GetStackFrames(StackTraceArguments args);
        IEnumerable<Thread> GetThreads();
        IEnumerable<Variable> GetVariables(VariablesArguments args);
        IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints);
        void SetDebugView(DebugView debugView);
        void SetExceptionBreakpoints(IReadOnlyList<string> filters);
        void Start();
        void Continue();
        void ReverseContinue();
        void StepIn();
        void StepOut();
        void StepOver();
        void StepBack();
    }
}
