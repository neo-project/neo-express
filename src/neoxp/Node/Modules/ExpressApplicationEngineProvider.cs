// Copyright (C) 2015-2024 The Neo Project.
//
// ExpressApplicationEngineProvider.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.SmartContract;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;

namespace NeoExpress.Node
{
    class ExpressApplicationEngineProvider : IApplicationEngineProvider
    {
        public ApplicationEngine? Create(TriggerType trigger, IVerifiable container, DataCache snapshot, Block persistingBlock, ProtocolSettings settings, long gas, IDiagnostic? diagnostic)
        {
            if (trigger == TriggerType.Application
                && container is Transaction tx
                && tx.Witnesses is not null
                && tx.Witnesses.Length > 0
                && EnumerateContractCalls(tx.Script).Any())
            {
                var path = Path.Combine(Environment.CurrentDirectory, $"{tx.Hash}.neo-trace");
                var sink = new TraceDebugStream(File.OpenWrite(path));
                return new TraceApplicationEngine(sink, trigger, container, snapshot, persistingBlock, settings, gas, diagnostic);
            }

            return null;
        }

        static IEnumerable<UInt160> EnumerateContractCalls(Script script)
        {
            var scriptHash = UInt160.Zero;
            foreach (var i in EnumerateInstructions(script))
            {
                if (i.OpCode == OpCode.PUSHDATA1
                    && i.Operand.Length == UInt160.Length)
                {
                    scriptHash = new UInt160(i.Operand.Span);
                }

                if (i.OpCode == OpCode.SYSCALL
                    && i.TokenU32 == ApplicationEngine.System_Contract_Call.Hash)
                {
                    yield return scriptHash;
                }
            }
        }

        static IEnumerable<Instruction> EnumerateInstructions(Script script)
        {
            var address = 0;
            var opcode = OpCode.PUSH0;
            while (address < script.Length)
            {
                var instruction = script.GetInstruction(address);
                opcode = instruction.OpCode;
                yield return instruction;
                address += instruction.Size;
            }

            if (opcode != OpCode.RET)
            {
                yield return Instruction.RET;
            }
        }

        public ApplicationEngine Create(TriggerType trigger, IVerifiable container, DataCache snapshot, Block persistingBlock, ProtocolSettings settings, long gas, IDiagnostic diagnostic, JumpTable jumpTable) => throw new NotImplementedException();
    }
}
