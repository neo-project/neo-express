// Copyright (C) 2015-2026 The Neo Project.
//
// DebugAdapter.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using System.IO;
using System.Threading;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// The Debug Adapter Protocol server for Neo smart contracts. Every request is delegated to an
    /// <see cref="IDebugSession"/>, which is built on launch by an injected <see cref="DebugSessionFactory"/>
    /// — so the adapter is independent of how a session is constructed (trace replay vs. live, and the
    /// launch-configuration parsing that selects between them).
    /// </summary>
    public class DebugAdapter : DebugAdapterBase
    {
        public delegate Task<IDebugSession> DebugSessionFactory(LaunchArguments launchArguments,
                                                                Action<DebugEvent> sendEvent,
                                                                DebugView defaultDebugView);

        /// <summary>Exception-breakpoint filter id for exceptions that are caught somewhere up the stack.</summary>
        public const string CAUGHT_EXCEPTION_FILTER = "caught";

        /// <summary>Exception-breakpoint filter id for exceptions with no catch block on the stack.</summary>
        public const string UNCAUGHT_EXCEPTION_FILTER = "uncaught";

        private class DebugViewRequest : DebugRequest<DebugViewArguments>
        {
            public DebugViewRequest() : base("debugview") { }
        }

        private class DebugViewArguments : DebugRequestArguments
        {
            [Newtonsoft.Json.JsonProperty("debugView")]
            public string DebugView { get; set; } = string.Empty;
        }

        private readonly Action<LogCategory, string> logger;
        private readonly DebugView defaultDebugView;
        private readonly DebugSessionFactory sessionFactory;
        private IDebugSession? session;
        private int launching; // 0 = idle, 1 = launching or launched

        public DebugAdapter(Stream @in,
                            Stream @out,
                            DebugSessionFactory sessionFactory,
                            Action<LogCategory, string>? logger = null,
                            DebugView defaultDebugView = DebugView.Source)
        {
            this.sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            this.logger = logger ?? ((_, __) => { });
            this.defaultDebugView = defaultDebugView;

            InitializeProtocolClient(@in, @out);
            Protocol.LogMessage += (sender, args) => this.logger(args.Category, args.Message);
            Protocol.RegisterRequestType<DebugViewRequest, DebugViewArguments>(a => HandleDebugViewRequest(a.Arguments));
        }

        public void Run()
        {
            Protocol.Run();
            Protocol.WaitForReader();
        }

        private void Log(string message, LogCategory category = LogCategory.DebugAdapterOutput) => logger(category, message);

        protected override InitializeResponse HandleInitializeRequest(InitializeArguments arguments)
        {
            return new InitializeResponse()
            {
                SupportsEvaluateForHovers = true,
                SupportsExceptionInfoRequest = true,
                ExceptionBreakpointFilters = new List<ExceptionBreakpointsFilter>
                {
                    new ExceptionBreakpointsFilter(CAUGHT_EXCEPTION_FILTER, "Caught Exceptions") { Default = false },
                    new ExceptionBreakpointsFilter(UNCAUGHT_EXCEPTION_FILTER, "Uncaught Exceptions") { Default = true },
                },
            };
        }

        protected override void HandleLaunchRequestAsync(IRequestResponder<LaunchArguments> responder)
        {
            _ = LaunchAsync(responder.Arguments).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    responder.SetResponse(new LaunchResponse());
                }
                else
                {
                    var exception = t.Exception is AggregateException aggregate && aggregate.InnerExceptions.Count == 1
                        ? aggregate.InnerExceptions[0]
                        : (Exception?)t.Exception;
                    responder.SetError(new ProtocolException(exception?.Message ?? "Unknown error launching the debug session.", exception));
                }
            }, TaskScheduler.Current);
        }

        internal async Task LaunchAsync(LaunchArguments arguments)
        {
            if (Interlocked.CompareExchange(ref launching, 1, 0) != 0)
                throw new InvalidOperationException("A debug session has already been launched.");

            try
            {
                session = await sessionFactory(arguments, Protocol.SendEvent, defaultDebugView).ConfigureAwait(false);
                session.Start();
                Protocol.SendEvent(new InitializedEvent());
            }
            catch
            {
                session = null;
                Interlocked.Exchange(ref launching, 0);
                throw;
            }
        }

        private void HandleDebugViewRequest(DebugViewArguments arguments)
        {
            try
            {
                session.AssertLaunched().SetDebugView(Enum.Parse<DebugView>(arguments.DebugView, true));
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                throw new ProtocolException(ex.Message, ex);
            }
        }

        protected override DisconnectResponse HandleDisconnectRequest(DisconnectArguments arguments)
            => new DisconnectResponse();

        protected override ExceptionInfoResponse HandleExceptionInfoRequest(ExceptionInfoArguments arguments)
            => Guard(() => new ExceptionInfoResponse() { Description = session.AssertLaunched().GetExceptionInfo() });

        protected override SourceResponse HandleSourceRequest(SourceArguments arguments)
            => Guard(() => session.AssertLaunched().GetSource(arguments));

        protected override ThreadsResponse HandleThreadsRequest(ThreadsArguments arguments)
            => Guard(() => new ThreadsResponse(session.AssertLaunched().GetThreads().ToList()));

        protected override StackTraceResponse HandleStackTraceRequest(StackTraceArguments arguments)
            => Guard(() => new StackTraceResponse(session.AssertLaunched().GetStackFrames(arguments).ToList()));

        protected override ScopesResponse HandleScopesRequest(ScopesArguments arguments)
            => Guard(() => new ScopesResponse(session.AssertLaunched().GetScopes(arguments).ToList()));

        protected override VariablesResponse HandleVariablesRequest(VariablesArguments arguments)
            => Guard(() => new VariablesResponse(session.AssertLaunched().GetVariables(arguments).ToList()));

        public static readonly EvaluateResponse FailedEvaluation = new EvaluateResponse()
        {
            PresentationHint = new VariablePresentationHint()
            {
                Attributes = VariablePresentationHint.AttributesValue.FailedEvaluation,
            },
        };

        protected override EvaluateResponse HandleEvaluateRequest(EvaluateArguments arguments)
        {
            try
            {
                return session.AssertLaunched().Evaluate(arguments);
            }
            catch (Exception)
            {
                return FailedEvaluation;
            }
        }

        protected override ContinueResponse HandleContinueRequest(ContinueArguments arguments)
            => Guard(() => { session.AssertLaunched().Continue(); return new ContinueResponse(); });

        protected override ReverseContinueResponse HandleReverseContinueRequest(ReverseContinueArguments arguments)
            => Guard(() => { session.AssertLaunched().ReverseContinue(); return new ReverseContinueResponse(); });

        protected override StepInResponse HandleStepInRequest(StepInArguments arguments)
            => Guard(() => { session.AssertLaunched().StepIn(); return new StepInResponse(); });

        protected override StepOutResponse HandleStepOutRequest(StepOutArguments arguments)
            => Guard(() => { session.AssertLaunched().StepOut(); return new StepOutResponse(); });

        // "Next" is "Step Over" in the VS Code UI.
        protected override NextResponse HandleNextRequest(NextArguments arguments)
            => Guard(() => { session.AssertLaunched().StepOver(); return new NextResponse(); });

        protected override StepBackResponse HandleStepBackRequest(StepBackArguments arguments)
            => Guard(() => { session.AssertLaunched().StepBack(); return new StepBackResponse(); });

        protected override SetBreakpointsResponse HandleSetBreakpointsRequest(SetBreakpointsArguments arguments)
            => Guard(() => new SetBreakpointsResponse(session.AssertLaunched().SetBreakpoints(arguments.Source, arguments.Breakpoints).ToList()));

        protected override SetExceptionBreakpointsResponse HandleSetExceptionBreakpointsRequest(SetExceptionBreakpointsArguments arguments)
            => Guard(() => { session.AssertLaunched().SetExceptionBreakpoints(arguments.Filters); return new SetExceptionBreakpointsResponse(); });

        private T Guard<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                throw new ProtocolException(ex.Message, ex);
            }
        }
    }

    internal static class DebugSessionExtensions
    {
        public static IDebugSession AssertLaunched(this IDebugSession? session)
            => session ?? throw new InvalidOperationException("No debug session has been launched.");
    }
}
