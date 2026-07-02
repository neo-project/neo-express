// Copyright (C) 2015-2026 The Neo Project.
//
// ExecutionContextContainer.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using StackItem = Neo.VM.Types.StackItem;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// Renders a frame's named arguments, locals and statics by pairing each VM slot with the matching
    /// debug-info variable (name, type and slot index), falling back to typeless rendering without debug info.
    /// </summary>
    internal class ExecutionContextContainer : IVariableContainer
    {
        private readonly IExecutionContext _context;
        private readonly DebugInfo? _debugInfo;

        public ExecutionContextContainer(IExecutionContext context, DebugInfo? debugInfo)
        {
            _context = context;
            _debugInfo = debugInfo;
        }

        public IEnumerable<Variable> Enumerate(IVariableManager manager)
        {
            DebugInfo.Method? method = _debugInfo.TryGetMethod(_context.InstructionPointer, out var foundMethod)
                ? foundMethod : null;

            var args = EnumerateSlot(manager, DebugSession.ARG_SLOTS_PREFIX, _context.Arguments, method?.Parameters);
            var locals = EnumerateSlot(manager, DebugSession.LOCAL_SLOTS_PREFIX, _context.LocalVariables, method?.Variables);
            var statics = EnumerateSlot(manager, DebugSession.STATIC_SLOTS_PREFIX, _context.StaticFields, _debugInfo?.StaticVariables);

            return args.Concat(locals).Concat(statics);

            static IEnumerable<Variable> EnumerateSlot(IVariableManager manager, string prefix, IReadOnlyList<StackItem>? slot, IReadOnlyList<DebugInfo.SlotVariable>? variableList = null)
            {
                variableList ??= Array.Empty<DebugInfo.SlotVariable>();
                slot ??= Array.Empty<StackItem>();

                for (int i = 0; i < variableList.Count; i++)
                {
                    var slotIndex = variableList[i].Index;
                    var type = Enum.TryParse<ContractParameterType>(variableList[i].Type, out var parsedType)
                        ? parsedType
                        : ContractParameterType.Any;
                    var item = slotIndex < slot.Count ? slot[slotIndex] : StackItem.Null;
                    var variable = item.ToVariable(manager, variableList[i].Name, type);
                    variable.EvaluateName = variable.Name;
                    yield return variable;
                }
            }
        }
    }
}
