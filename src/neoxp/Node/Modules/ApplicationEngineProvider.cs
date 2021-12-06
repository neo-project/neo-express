using System;
using System.Collections.Generic;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit.SmartContract;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.VM;

namespace NeoExpress.Node
{
    // Note: namespace alias needed to avoid conflict with Plugin.System property
    using SysIO = System.IO;

    class ApplicationEngineProvider : Plugin, IApplicationEngineProvider
    {
        public ApplicationEngine? Create(TriggerType trigger, IVerifiable container, DataCache snapshot, Block persistingBlock, ProtocolSettings settings, long gas, Diagnostic? diagnostic)
        {
            if (trigger == TriggerType.Application
                && container is Transaction tx
                && tx.Witnesses != null
                && tx.Witnesses.Length > 0
                && EnumerateContractCalls(tx.Script).Any())
            {
                var path = SysIO.Path.Combine(Environment.CurrentDirectory, $"{tx.Hash}.neo-trace");
                var sink = new TraceDebugStream(SysIO.File.OpenWrite(path));
                return new TraceApplicationEngine(sink, trigger, container, snapshot, persistingBlock, settings, gas, diagnostic);
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
                    && i.TokenU32 == ApplicationEngine.System_Contract_Call.Hash)
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
