using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.SmartContract;
using Neo.BlockchainToolkit.TraceDebug;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract;
using Neo.VM;

namespace NeoExpress.Node
{
    partial class ExpressSystem
    {
        class ApplicationEngineProviderPlugin : Plugin, IApplicationEngineProvider
        {
            public ApplicationEngine? Create(TriggerType trigger, IVerifiable container, DataCache snapshot, Block persistingBlock, ProtocolSettings settings, long gas, Diagnostic diagnostic)
            {
                if (trigger == TriggerType.Application
                    && container is Transaction tx
                    && tx.Witnesses != null
                    && tx.Witnesses.Length > 0
                    && EnumerateContractCalls(tx.Script).Any())
                {
                    var path = System.IO.Path.Combine(Environment.CurrentDirectory, $"{tx.Hash}.neo-trace");
                    var sink = new TraceDebugStream(File.OpenWrite(path));
                    return new TraceApplicationEngine(sink, trigger, container, snapshot, persistingBlock, settings, gas, diagnostic);
                }

                return null;
            }

            private static IEnumerable<UInt160> EnumerateContractCalls(Script script)
            {
                var scriptHash = UInt160.Zero;
                foreach (var (_, instruction) in script.EnumerateInstructions())
                {
                    if (instruction.OpCode == OpCode.PUSHDATA1
                        && instruction.Operand.Length == UInt160.Length)
                    {
                        scriptHash = new UInt160(instruction.Operand.Span);
                    }

                    if (instruction.OpCode == OpCode.SYSCALL
                        && instruction.TokenU32 == ApplicationEngine.System_Contract_Call.Hash)
                    {
                        yield return scriptHash;
                    }
                }
            }
        }
    }
}
