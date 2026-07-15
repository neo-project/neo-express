// Copyright (C) 2015-2026 The Neo Project.
//
// DebugAdapterLifecycleTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using NeoDebug.Neo3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Thread = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Thread;

namespace test.neodebug
{
    // Verifies the DAP layer: capabilities, the launch lifecycle (the injected factory builds the session
    // and the session is started), and that requests delegate to IDebugSession or fail before launch.
    public class DebugAdapterLifecycleTests
    {
        private sealed class FakeDebugSession : IDebugSession
        {
            public bool Started, Continued, SteppedOver, Disposed;

            public void Dispose() => Disposed = true;
            public void Start() => Started = true;
            public IEnumerable<Thread> GetThreads() => new[] { new Thread(1, "main") };
            public IEnumerable<StackFrame> GetStackFrames(StackTraceArguments args) => new[] { new StackFrame(0, "Run", 6, 0) };
            public void Continue() => Continued = true;
            public void StepOver() => SteppedOver = true;

            public EvaluateResponse Evaluate(EvaluateArguments args) => new EvaluateResponse();
            public string GetExceptionInfo() => string.Empty;
            public IEnumerable<Scope> GetScopes(ScopesArguments args) => Array.Empty<Scope>();
            public SourceResponse GetSource(SourceArguments arguments) => new SourceResponse();
            public IEnumerable<Variable> GetVariables(VariablesArguments args) => Array.Empty<Variable>();
            public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints) => Array.Empty<Breakpoint>();
            public void SetDebugView(DebugView debugView) { }
            public void SetExceptionBreakpoints(IReadOnlyList<string> filters) { }
            public void ReverseContinue() { }
            public void StepIn() { }
            public void StepOut() { }
            public void StepBack() { }
        }

        private sealed class TestDebugAdapter : DebugAdapter
        {
            public TestDebugAdapter(IDebugSession session)
                : base(Stream.Null, Stream.Null, (_, __, ___) => Task.FromResult(session))
            {
            }

            public TestDebugAdapter(DebugSessionFactory sessionFactory)
                : base(Stream.Null, Stream.Null, sessionFactory)
            {
            }

            public new InitializeResponse HandleInitializeRequest(InitializeArguments arguments) => base.HandleInitializeRequest(arguments);
            public ThreadsResponse Threads() => HandleThreadsRequest(new ThreadsArguments());
            public StackTraceResponse StackTrace() => HandleStackTraceRequest(new StackTraceArguments());
            public ContinueResponse Continue() => HandleContinueRequest(new ContinueArguments());
            public ConfigurationDoneResponse ConfigurationDone() => HandleConfigurationDoneRequest(new ConfigurationDoneArguments());
            public DisconnectResponse Disconnect() => HandleDisconnectRequest(new DisconnectArguments());
            public NextResponse Next() => HandleNextRequest(new NextArguments());
            public Task Launch() => LaunchAsync(new LaunchArguments());
        }

        [Fact]
        public void initialize_advertises_capabilities()
        {
            var adapter = new TestDebugAdapter(new FakeDebugSession());

            var response = adapter.HandleInitializeRequest(new InitializeArguments());

            Assert.True(response.SupportsEvaluateForHovers);
            Assert.True(response.SupportsExceptionInfoRequest);
            Assert.True(response.SupportsConfigurationDoneRequest);
            Assert.Equal(2, response.ExceptionBreakpointFilters.Count);
            Assert.Contains(response.ExceptionBreakpointFilters, f => f.Filter == DebugAdapter.UNCAUGHT_EXCEPTION_FILTER && f.Default == true);
            Assert.Contains(response.ExceptionBreakpointFilters, f => f.Filter == DebugAdapter.CAUGHT_EXCEPTION_FILTER && f.Default == false);
        }

        [Fact]
        public void requests_before_launch_throw_protocol_exception()
        {
            var adapter = new TestDebugAdapter(new FakeDebugSession());

            Assert.Throws<ProtocolException>(() => adapter.Threads());
        }

        [Fact]
        public async Task configuration_done_starts_the_launched_session()
        {
            var session = new FakeDebugSession();
            var adapter = new TestDebugAdapter(session);

            await adapter.Launch();

            Assert.False(session.Started);
            adapter.ConfigurationDone();
            Assert.True(session.Started);

            var threads = adapter.Threads().Threads;
            Assert.Single(threads);
            Assert.Equal(1, threads[0].Id);
            Assert.Equal("main", threads[0].Name);
        }

        [Fact]
        public async Task requests_delegate_to_the_session()
        {
            var session = new FakeDebugSession();
            var adapter = new TestDebugAdapter(session);
            await adapter.Launch();
            adapter.ConfigurationDone();

            adapter.Continue();
            adapter.Next();
            var frames = adapter.StackTrace().StackFrames;

            Assert.True(session.Continued);
            Assert.True(session.SteppedOver);
            Assert.Single(frames);
            Assert.Equal(6, frames[0].Line);
        }

        [Fact]
        public async Task launch_rejects_concurrent_requests()
        {
            var session = new FakeDebugSession();
            var factoryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFactory = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int factoryCalls = 0;

            var adapter = new TestDebugAdapter(async (_, __, ___) =>
            {
                Interlocked.Increment(ref factoryCalls);
                factoryStarted.SetResult();
                await releaseFactory.Task;
                return session;
            });

            Task firstLaunch = adapter.Launch();
            await factoryStarted.Task;

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.Launch());
            releaseFactory.SetResult();
            await firstLaunch;

            Assert.Equal("A debug session has already been launched.", exception.Message);
            Assert.Equal(1, factoryCalls);
            Assert.False(session.Started);
        }

        [Fact]
        public async Task disconnect_disposes_the_session()
        {
            var session = new FakeDebugSession();
            var adapter = new TestDebugAdapter(session);
            await adapter.Launch();
            adapter.ConfigurationDone();

            adapter.Disconnect();

            Assert.True(session.Disposed);
        }
    }
}
