using System;
using System.Collections.Generic;
using System.Linq;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.VM;

namespace NeoExpress.Neo3.Node
{
    using SysIO = System.IO;

    internal class ExpressApplicationEngineProvider : Plugin, IApplicationEngineProvider
    {
        public ApplicationEngine? Create(TriggerType trigger, IVerifiable container, StoreView snapshot, long gas)
        {
            if (trigger == TriggerType.Application
                && container is Transaction tx
                && EnumerateContractCalls(tx.Script).Any())
            {
                var path = SysIO.Path.Combine(Environment.CurrentDirectory, $"{tx.Hash}.neo-trace");
                var sink = new TraceDebugSink(SysIO.File.OpenWrite(path));
                return new ExpressApplicationEngine(sink, trigger, container, snapshot, gas);
            }

            return null;
        }

        private static IEnumerable<UInt160> EnumerateContractCalls(Script script)
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
                    && ((i.TokenU32 == ApplicationEngine.System_Contract_Call.Hash)
                        || (i.TokenU32 == ApplicationEngine.System_Contract_CallEx.Hash)))
                {
                    yield return scriptHash;
                }
            }
        }

        private static IEnumerable<Instruction> EnumerateInstructions(Script script)
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
    }
}
