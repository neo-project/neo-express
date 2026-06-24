// Copyright (C) 2015-2026 The Neo Project.
//
// DebugSession.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Extensions;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using ByteString = Neo.VM.Types.ByteString;
using NeoArray = Neo.VM.Types.Array;
using Script = Neo.VM.Script;
using StackItem = Neo.VM.Types.StackItem;
using StackItemType = Neo.VM.Types.StackItemType;
using Thread = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages.Thread;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// Drives an <see cref="IApplicationEngine"/> on behalf of the DAP adapter: stepping (forward and, for the
    /// trace-replay engine, backward), source/disassembly stack frames, scopes and variables, breakpoints,
    /// exception breakpoints, and expression evaluation. The slot/storage prefix constants live in the
    /// companion partial.
    /// </summary>
    internal partial class DebugSession : IDebugSession, IDisposable
    {
        private readonly IApplicationEngine engine;
        private readonly IReadOnlyList<CastOperation> returnTypes;
        private readonly IReadOnlyDictionary<UInt160, DebugInfo> debugInfoMap;
        private readonly Action<DebugEvent> sendEvent;
        private bool disassemblyView;
        private readonly DisassemblyManager disassemblyManager;
        private readonly VariableManager variableManager = new();
        private readonly BreakpointManager breakpointManager;
        private bool breakOnCaughtExceptions;
        private bool breakOnUncaughtExceptions = true;

        public DebugSession(IApplicationEngine engine, IReadOnlyList<DebugInfo> debugInfoList, IReadOnlyList<CastOperation> returnTypes, Action<DebugEvent> sendEvent, DebugView defaultDebugView)
        {
            this.engine = engine;
            this.returnTypes = returnTypes;
            this.sendEvent = sendEvent;
            debugInfoMap = debugInfoList.ToDictionary(di => di.ScriptHash);
            disassemblyView = defaultDebugView == DebugView.Disassembly;
            disassemblyManager = new DisassemblyManager(TryGetScript, debugInfoMap.TryGetValue);
            breakpointManager = new BreakpointManager(disassemblyManager, debugInfoList);

            this.engine.DebugNotify += OnNotify;
            this.engine.DebugLog += OnLog;

            if (engine.SupportsStepBack)
            {
                sendEvent(new CapabilitiesEvent
                {
                    Capabilities = new Capabilities { SupportsStepBack = true },
                });
            }
        }

        private void OnNotify(object? sender, (UInt160 scriptHash, string scriptName, string eventName, NeoArray state) args)
        {
            var state = args.state.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
            sendEvent(new OutputEvent()
            {
                Output = $"Runtime.Notify: {(string.IsNullOrEmpty(args.scriptName) ? args.scriptHash.ToString() : args.scriptName)} {args.eventName} {state}\n",
            });
        }

        private void OnLog(object? sender, (UInt160 scriptHash, string scriptName, string message) args)
        {
            sendEvent(new OutputEvent()
            {
                Output = $"Runtime.Log: {(string.IsNullOrEmpty(args.scriptName) ? args.scriptHash.ToString() : args.scriptName)} {args.message}\n",
            });
        }

        public void Dispose()
        {
            engine.Dispose();
        }

        private bool TryGetScript(UInt160 scriptHash, [MaybeNullWhen(false)] out Script script)
        {
            if (engine.TryGetContract(scriptHash, out var contractScript))
            {
                script = contractScript;
                return true;
            }

            foreach (var context in engine.InvocationStack)
            {
                if (scriptHash == context.ScriptIdentifier)
                {
                    script = context.Script;
                    return true;
                }
            }

            script = null;
            return false;
        }

        public void Start()
        {
            variableManager.Clear();

            if (disassemblyView)
            {
                FireStoppedEvent(StoppedEvent.ReasonValue.Entry);
            }
            else
            {
                StepIn();
            }
        }

        public IEnumerable<Thread> GetThreads()
        {
            yield return new Thread(1, "main thread");
        }

        public IEnumerable<StackFrame> GetStackFrames(StackTraceArguments args)
        {
            System.Diagnostics.Debug.Assert(args.ThreadId == 1);

            foreach (var (context, index) in engine.InvocationStack.Select((c, i) => (c, i)))
            {
                DebugInfo.Method? method = debugInfoMap.TryGetValue(context.ScriptIdentifier, out var debugInfo)
                    && debugInfo.TryGetMethod(context.InstructionPointer, out var foundMethod)
                        ? foundMethod : null;

                var frame = new StackFrame()
                {
                    Id = index,
                    Name = method?.Name ?? $"frame {engine.InvocationStack.Count - index}",
                };

                if (disassemblyView)
                {
                    var disassembly = disassemblyManager.GetDisassembly(context, debugInfo);
                    frame.Source = new Source()
                    {
                        SourceReference = disassembly.SourceReference,
                        Name = disassembly.Name,
                        Path = disassembly.Name,
                    };
                    frame.Line = disassembly.AddressMap[context.InstructionPointer];
                    frame.Column = 1;
                }
                else
                {
                    if (method.TryGetCurrentSequencePoint(context.InstructionPointer, out var sequencePoint))
                    {
                        if (sequencePoint.TryGetDocumentPath(debugInfo, out var document))
                        {
                            frame.Source = new Source()
                            {
                                Name = System.IO.Path.GetFileName(document),
                                Path = document,
                            };
                        }

                        frame.Line = sequencePoint.Start.Line;
                        frame.Column = sequencePoint.Start.Column;

                        if (sequencePoint.Start != sequencePoint.End)
                        {
                            frame.EndLine = sequencePoint.End.Line;
                            frame.EndColumn = sequencePoint.End.Column;
                        }
                    }
                }

                yield return frame;
            }
        }

        public IEnumerable<Scope> GetScopes(ScopesArguments args)
        {
            var context = engine.InvocationStack.ElementAt(args.FrameId);
            var scriptId = context.ScriptHash;

            if (disassemblyView)
            {
                yield return AddScope("Evaluation Stack", new SlotContainer(EVAL_STACK_PREFIX, context.EvaluationStack));
                yield return AddScope("Arguments", new SlotContainer(ARG_SLOTS_PREFIX, context.Arguments));
                yield return AddScope("Locals", new SlotContainer(LOCAL_SLOTS_PREFIX, context.LocalVariables));
                yield return AddScope("Statics", new SlotContainer(STATIC_SLOTS_PREFIX, context.StaticFields));
                yield return AddScope("Result Stack", new SlotContainer(RESULT_STACK_PREFIX, engine.ResultStack));
            }
            else
            {
                var debugInfo = debugInfoMap.TryGetValue(context.ScriptIdentifier, out var foundDebugInfo) ? foundDebugInfo : null;
                var container = new ExecutionContextContainer(context, debugInfo);
                yield return AddScope("Variables", container);
            }

            yield return AddScope("Storage", new StorageContainer(engine.GetStorages(scriptId)));
            yield return AddScope("Engine", new EngineContainer(engine));

            Scope AddScope(string name, IVariableContainer container)
            {
                var reference = variableManager.Add(container);
                return new Scope(name, reference, false);
            }
        }

        public IEnumerable<Variable> GetVariables(VariablesArguments args)
        {
            if (variableManager.TryGet(args.VariablesReference, out var container))
            {
                return container.Enumerate(variableManager);
            }

            return Enumerable.Empty<Variable>();
        }

        public SourceResponse GetSource(SourceArguments arguments)
        {
            if (disassemblyManager.TryGetDisassembly(arguments.SourceReference, out var disassembly))
            {
                return new SourceResponse(disassembly.Source)
                {
                    MimeType = "text/x-neovm.disassembly",
                };
            }

            throw new InvalidOperationException();
        }

        private (StackItem? item, ContractParameterType type, ReadOnlyMemory<char> remaining) Evaluate(IExecutionContext context, ReadOnlyMemory<char> text)
        {
            if (text.StartsWith(STORAGE_PREFIX))
            {
                var container = new StorageContainer(engine.GetStorages(context.ScriptHash));
                var storage = container.Evaluate(text);
                return (storage.item, ContractParameterType.Any, storage.remaining);
            }

            var (name, remaining) = ParseVariableName(text);

            if (name.Length > 0 && name.Span[0] == '#')
            {
                StackItem? item;
                if (TryEvaluateIndexedSlot(name, ARG_SLOTS_PREFIX, context.Arguments, out item)) return (item, ContractParameterType.Any, remaining);
                if (TryEvaluateIndexedSlot(name, EVAL_STACK_PREFIX, context.EvaluationStack, out item)) return (item, ContractParameterType.Any, remaining);
                if (TryEvaluateIndexedSlot(name, LOCAL_SLOTS_PREFIX, context.LocalVariables, out item)) return (item, ContractParameterType.Any, remaining);
                if (TryEvaluateIndexedSlot(name, RESULT_STACK_PREFIX, engine.ResultStack, out item)) return (item, ContractParameterType.Any, remaining);
                if (TryEvaluateIndexedSlot(name, STATIC_SLOTS_PREFIX, context.StaticFields, out item)) return (item, ContractParameterType.Any, remaining);
            }

            if (debugInfoMap.TryGetValue(context.ScriptIdentifier, out var debugInfo))
            {
                if (TryEvaluateNamedSlot(context.StaticFields, debugInfo.StaticVariables, name, out var result))
                {
                    return (result.item, result.type, remaining);
                }

                if (debugInfo.TryGetMethod(context.InstructionPointer, out var method))
                {
                    if (TryEvaluateNamedSlot(context.Arguments, method.Parameters, name, out result))
                    {
                        return (result.item, result.type, remaining);
                    }

                    if (TryEvaluateNamedSlot(context.LocalVariables, method.Variables, name, out result))
                    {
                        return (result.item, result.type, remaining);
                    }
                }
            }

            return default;

            static bool TryEvaluateIndexedSlot(ReadOnlyMemory<char> name, string prefix, IReadOnlyList<StackItem> slot, [MaybeNullWhen(false)] out StackItem result)
            {
                if (name.Span.Length > prefix.Length
                    && name.Span.Slice(0, prefix.Length).SequenceEqual(prefix))
                {
                    var index = int.TryParse(name.Span.Slice(prefix.Length), out int parsedIndex) ? parsedIndex : int.MaxValue;

                    if (index < slot.Count)
                    {
                        result = slot[index];
                        return true;
                    }
                }

                result = default;
                return false;
            }

            static bool TryEvaluateNamedSlot(IReadOnlyList<StackItem> slot, IReadOnlyList<DebugInfo.SlotVariable> variableList, ReadOnlyMemory<char> name, out (StackItem? item, ContractParameterType type) result)
            {
                for (int i = 0; i < variableList.Count; i++)
                {
                    if (name.Span.SequenceEqual(variableList[i].Name)
                        && variableList[i].Index < slot.Count)
                    {
                        var type = Enum.TryParse<ContractParameterType>(variableList[i].Type, out var parsedType)
                            ? parsedType
                            : ContractParameterType.Any;
                        result = (slot[variableList[i].Index], type);
                        return true;
                    }
                }

                result = default;
                return false;
            }
        }

        private static (StackItem? item, ContractParameterType type, ReadOnlyMemory<char> remaining) Evaluate(StackItem? item, ContractParameterType type, ReadOnlyMemory<char> remaining)
        {
            if (remaining.IsEmpty) throw new ArgumentException("Evaluate remaining argument IsEmpty", nameof(remaining));

            if (remaining.Span[0] == '[')
            {
                var bracketIndex = remaining.Span.IndexOf(']');
                if (bracketIndex >= 0
                    && int.TryParse(remaining[1..bracketIndex].Span, out var index))
                {
                    var newItem = item switch
                    {
                        Neo.VM.Types.Buffer buffer => (int)buffer.InnerBuffer.Span[index],
                        ByteString byteString => (int)byteString.GetSpan()[index],
                        NeoArray array => array[index],
                        _ => throw new InvalidOperationException(),
                    };

                    return (newItem, ContractParameterType.Any, remaining.Slice(bracketIndex + 1));
                }
            }
            else if (remaining.Span[0] == '.')
            {
                throw new NotImplementedException();
            }

            throw new InvalidOperationException();
        }

        public static readonly IReadOnlyDictionary<string, CastOperation> CastOperations = new Dictionary<string, CastOperation>()
        {
            { "int", CastOperation.Integer },
            { "integer", CastOperation.Integer },
            { "bool", CastOperation.Boolean },
            { "boolean", CastOperation.Boolean },
            { "string", CastOperation.String },
            { "str", CastOperation.String },
            { "hex", CastOperation.HexString },
            { "byte[]", CastOperation.ByteArray },
            { "addr", CastOperation.Address },
        }.ToImmutableDictionary();

        private static (CastOperation castOperation, ReadOnlyMemory<char> remaining) ParseCastOperation(string expression)
        {
            if (expression[0] == '(')
            {
                foreach (var kvp in CastOperations)
                {
                    if (expression.Length > kvp.Key.Length + 2
                        && expression.AsSpan().Slice(1, kvp.Key.Length).SequenceEqual(kvp.Key)
                        && expression[kvp.Key.Length + 1] == ')')
                    {
                        return (kvp.Value, expression.AsMemory().Slice(kvp.Key.Length + 2));
                    }
                }

                throw new Exception("invalid cast operation");
            }

            return (CastOperation.None, expression.AsMemory());
        }

        private static (ReadOnlyMemory<char> name, ReadOnlyMemory<char> remaining) ParseVariableName(ReadOnlyMemory<char> expression)
        {
            for (int i = 0; i < expression.Length; i++)
            {
                char c = expression.Span[i];
                if (c == '.' || c == '[')
                {
                    return (expression.Slice(0, i), expression.Slice(i));
                }
            }

            return (expression, default);
        }

        private (string value, int variablesReference) EvaluateVariable(StackItem item, ContractParameterType parameterType, CastOperation castOperation)
        {
            var value = castOperation switch
            {
                CastOperation.Address => ToAddress(item, engine.AddressVersion),
                CastOperation.HexString => ToHexString(item),
                _ => null,
            };

            if (value != null) return (value, 0);

            var variable = castOperation switch
            {
                CastOperation.Boolean => item.ToVariable(variableManager, string.Empty, ContractParameterType.Boolean),
                CastOperation.ByteArray => item.ToVariable(variableManager, string.Empty, ContractParameterType.ByteArray),
                CastOperation.Integer => item.ToVariable(variableManager, string.Empty, ContractParameterType.Integer),
                CastOperation.String => item.ToVariable(variableManager, string.Empty, ContractParameterType.String),
                _ => null,
            };

            variable ??= item.ToVariable(variableManager, string.Empty, parameterType);
            return (variable.Value, variable.VariablesReference);

            static string? ToAddress(StackItem item, byte version)
            {
                try
                {
                    var span = item.GetSpan();
                    if (span.Length == UInt160.Length)
                    {
                        var uint160 = new UInt160(span);
                        return uint160.ToAddress(version);
                    }
                }
                catch { }

                return null;
            }

            static string? ToHexString(StackItem item)
            {
                try
                {
                    return item.IsNull
                        ? "<null>"
                        : ((ByteString)item.ConvertTo(StackItemType.ByteString)).GetSpan().ToHexString();
                }
                catch { }

                return null;
            }
        }

        public EvaluateResponse Evaluate(EvaluateArguments args)
        {
            if (!args.FrameId.HasValue)
                return DebugAdapter.FailedEvaluation;

            try
            {
                var (castOperation, text) = ParseCastOperation(args.Expression);
                var (item, type, remaining) = Evaluate(engine.InvocationStack.ElementAt(args.FrameId.Value), text);
                while (!remaining.IsEmpty)
                {
                    (item, type, remaining) = Evaluate(item, type, remaining);
                }

                if (item != null)
                {
                    var (result, variablesRef) = EvaluateVariable(item, type, castOperation);
                    return new EvaluateResponse(result, variablesRef);
                }

                return DebugAdapter.FailedEvaluation;
            }
            catch (Exception)
            {
                return DebugAdapter.FailedEvaluation;
            }
        }

        public IEnumerable<Breakpoint> SetBreakpoints(Source source, IReadOnlyList<SourceBreakpoint> sourceBreakpoints)
        {
            return breakpointManager.SetBreakpoints(source, sourceBreakpoints);
        }

        public void SetExceptionBreakpoints(IReadOnlyList<string> filters)
        {
            breakOnCaughtExceptions = filters.Any(f => f == DebugAdapter.CAUGHT_EXCEPTION_FILTER);
            breakOnUncaughtExceptions = filters.Any(f => f == DebugAdapter.UNCAUGHT_EXCEPTION_FILTER);
        }

        public string GetExceptionInfo()
        {
            var item = engine.CurrentContext?.EvaluationStack[0];
            return item?.GetString() ?? throw new InvalidOperationException("missing exception information");
        }

        public void SetDebugView(DebugView debugView)
        {
            var original = disassemblyView;
            disassemblyView = debugView switch
            {
                DebugView.Disassembly => true,
                DebugView.Source => false,
                DebugView.Toggle => !disassemblyView,
                _ => throw new ArgumentException($"Unknown DebugView {debugView}", nameof(debugView)),
            };

            if (original != disassemblyView)
            {
                FireStoppedEvent(StoppedEvent.ReasonValue.Step);
            }
        }

        private void FireStoppedEvent(StoppedEvent.ReasonValue reasonValue)
        {
            variableManager.Clear();
            sendEvent(new StoppedEvent(reasonValue) { ThreadId = 1 });
        }

        private int lastThrowAddress = -1;

        private string ToResult(int index)
        {
            if (index >= engine.ResultStack.Count) throw new ArgumentException("invalid ResultStack index", nameof(index));

            var result = engine.ResultStack[index];
            var returnType = index < returnTypes.Count ? returnTypes[index] : CastOperation.None;
            var (value, variablesRef) = EvaluateVariable(result, ContractParameterType.Any, returnType);

            return variablesRef == 0
                ? value
                : result.ToJson().ToString(Newtonsoft.Json.Formatting.Indented);
        }

        private void Step(Func<int, bool> compareStepDepth, bool stepBack = false)
        {
            while (true)
            {
                if (stepBack && engine.AtStart)
                {
                    sendEvent(new OutputEvent()
                    {
                        Category = OutputEvent.CategoryValue.Stdout,
                        Output = "Reached start of trace",
                    });
                    break;
                }

                if (!stepBack && engine.State == VMState.FAULT)
                {
                    var output = "Engine State Faulted\n";
                    if (engine.FaultException != null)
                    {
                        output = $"Engine State Fault: {engine.FaultException.Message} [{engine.FaultException.GetType().Name}]\n";
                        if (engine.FaultException.InnerException != null)
                        {
                            output += $"  Contract Exception: {engine.FaultException.InnerException.Message} [{engine.FaultException.InnerException.GetType().Name}]\n";
                        }
                    }
                    output += $"Gas Consumed: {engine.GasConsumed.AsBigDecimal()}\n";

                    sendEvent(new OutputEvent()
                    {
                        Category = OutputEvent.CategoryValue.Stderr,
                        Output = output,
                    });
                    sendEvent(new TerminatedEvent());
                    break;
                }

                if (!stepBack && engine.State == VMState.HALT)
                {
                    for (int index = 0; index < engine.ResultStack.Count; index++)
                    {
                        var result = ToResult(index);
                        sendEvent(new OutputEvent()
                        {
                            Category = OutputEvent.CategoryValue.Stdout,
                            Output = $"Gas Consumed: {engine.GasConsumed.AsBigDecimal()}\nReturn: {result}\n",
                        });
                    }
                    sendEvent(new ExitedEvent());
                    sendEvent(new TerminatedEvent());
                    break;
                }

                if (stepBack)
                {
                    lastThrowAddress = -1;
                    if (!engine.ExecutePrevInstruction())
                        break;
                }
                else
                {
                    // The VM provides no way to halt after an exception is thrown but before it is handled, so
                    // the debugger checks whether the engine is about to THROW and applies the exception
                    // breakpoints itself. lastThrowAddress avoids re-checking the same THROW address in a row.
                    if (engine.CurrentContext?.CurrentInstruction?.OpCode == OpCode.THROW
                        && engine.CurrentContext.InstructionPointer != lastThrowAddress)
                    {
                        lastThrowAddress = engine.CurrentContext.InstructionPointer;
                        var handled = engine.CatchBlockOnStack();
                        if ((handled && breakOnCaughtExceptions)
                            || (!handled && breakOnUncaughtExceptions))
                        {
                            FireStoppedEvent(StoppedEvent.ReasonValue.Exception);
                            break;
                        }
                    }
                    else
                    {
                        lastThrowAddress = -1;
                        if (!engine.ExecuteNextInstruction())
                            break;
                    }
                }

                if (breakpointManager.CheckBreakpoint(engine.CurrentContext?.ScriptHash ?? UInt160.Zero, engine.CurrentContext?.InstructionPointer))
                {
                    FireStoppedEvent(StoppedEvent.ReasonValue.Breakpoint);
                    break;
                }

                if (compareStepDepth(engine.InvocationStack.Count)
                    && (disassemblyView || CheckSequencePoint()))
                {
                    FireStoppedEvent(StoppedEvent.ReasonValue.Step);
                    break;
                }
            }

            bool CheckSequencePoint()
            {
                if (engine.CurrentContext != null)
                {
                    var ip = engine.CurrentContext.InstructionPointer;
                    if (debugInfoMap.TryGetValue(engine.CurrentContext.ScriptIdentifier, out var info))
                    {
                        var methods = info.Methods;
                        for (int i = 0; i < methods.Count; i++)
                        {
                            if (methods[i].SequencePoints.Any(sp => sp.Address == ip))
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
        }

        public void Continue()
        {
            Step((_) => false);
        }

        public void ReverseContinue()
        {
            Step((_) => false, true);
            // In source view, if we've stepped back before the first sequence point, step forward to it.
            if (!disassemblyView && engine.AtStart) StepIn();
        }

        public void StepIn()
        {
            Step((_) => true);
        }

        public void StepOut()
        {
            var originalStackCount = engine.InvocationStack.Count;
            Step((currentStackCount) => currentStackCount < originalStackCount);
        }

        public void StepOver()
        {
            var originalStackCount = engine.InvocationStack.Count;
            Step((currentStackCount) => currentStackCount <= originalStackCount);
        }

        public void StepBack()
        {
            Step((_) => true, true);
        }
    }
}
