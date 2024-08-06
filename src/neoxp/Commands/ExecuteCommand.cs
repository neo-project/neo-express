// Copyright (C) 2015-2024 The Neo Project.
//
// ExecuteCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Text;

namespace NeoExpress.Commands
{
    using McMaster.Extensions.CommandLineUtils;
    using Neo.Extensions;

    [Command(Name = "execute", Description = "Invoke a custom script, the input text will be converted to script with a priority: hex, base64, file path.")]
    class ExecuteCommand
    {

        readonly ExpressChainManagerFactory chainManagerFactory;
        readonly TransactionExecutorFactory txExecutorFactory;

        public ExecuteCommand(ExpressChainManagerFactory chainManagerFactory, TransactionExecutorFactory txExecutorFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
            this.txExecutorFactory = txExecutorFactory;
        }

        [Argument(0, Description = "A neo-vm script (Format: HEX, BASE64, Filename)")]
        [Required]
        internal string InputText { get; set; } = string.Empty;

        [Option(Description = "Account to pay invocation GAS fee")]
        internal string Account { get; init; } = string.Empty;

        [Option(Description = "Witness Scope for transaction signer(s) (Allowed: None, CalledByEntry, Global)")]
        [AllowedValues(StringComparison.OrdinalIgnoreCase, "None", "CalledByEntry", "Global")]
        internal WitnessScope WitnessScope { get; init; } = WitnessScope.CalledByEntry;

        [Option(Description = "Invoke contract for results (does not cost GAS)")]
        internal bool Results { get; init; } = false;

        [Option("--gas|-g", CommandOptionType.SingleValue, Description = "Additional GAS to apply to the contract invocation")]
        internal decimal AdditionalGas { get; init; } = 0;

        [Option(Description = "password to use for NEP-2/NEP-6 account")]
        internal string Password { get; init; } = string.Empty;

        [Option(Description = "Enable contract execution tracing")]
        internal bool Trace { get; init; } = false;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;


        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                if (string.IsNullOrEmpty(Account) && !Results)
                {
                    throw new Exception("Either --account or --results must be specified");
                }

                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                using var txExec = txExecutorFactory.Create(chainManager, Trace, false);

                var script = ConvertTextToScript(InputText) ?? LoadFileScript(InputText);
                if (script == null)
                {
                    console.WriteLine($"Invalid script: {InputText}");
                    return 1;
                }

                if (Results)
                {
                    await txExec.InvokeForResultsAsync(script, Account, WitnessScope);
                }
                else
                {
                    var password = chainManager.Chain.ResolvePassword(Account, Password);
                    await txExec.ContractInvokeAsync(script, Account, password, WitnessScope, AdditionalGas);
                }

                console.WriteLine("Opcodes:");
                foreach (var opInfo in GetInstructionString(script))
                {
                    console.WriteLine(opInfo);
                }
                return 0;
            }
            catch (Exception ex)
            {
                app.WriteException(ex, showInnerExceptions: true);
                return 1;
            }
        }



        private static Script? LoadFileScript(string fileName)
        {
            var fileText = File.ReadAllText(fileName);
            var txScript = ConvertTextToScript(fileText);
            if (txScript != null)
            {
                return txScript;

            }
            var file = File.ReadAllBytes(fileName);
            try
            {
                var nef = file.AsSerializable<NefFile>();
                return nef?.Script;
            }
            catch (Exception)
            {
            }
            return null;
        }


        private static Script? ConvertTextToScript(string inputScript)
        {
            if (inputScript.TryGetBytesFromHexString(out var hexBytes) && TryConvertBytesToScript(hexBytes, out var s1))
            {
                return s1;
            }
            if (inputScript.TryGetBytesFromBase64String(out var b64Bytes) && TryConvertBytesToScript(b64Bytes, out var s2))
            {
                return s2;
            }
            return null;
        }


        private static bool TryConvertBytesToScript(Span<byte> bytes, out Script? script)
        {
            try
            {
                script = new Script(bytes.ToArray(), true);
                return true;
            }
            catch (Exception)
            {
                script = null;
                return false;
            }
        }

        private static IEnumerable<string> GetInstructionString(Script script)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            foreach (var (offset, instruction) in script.EnumerateInstructions())
            {
                if (offset == script.Length)
                    break;
                sb.Append($"[{offset}]:{instruction.OpCode}");
                switch (instruction.OpCode)
                {
                    case OpCode.PUSHINT8:
                    case OpCode.PUSHINT16:
                    case OpCode.PUSHINT32:
                    case OpCode.PUSHINT64:
                    case OpCode.PUSHINT128:
                    case OpCode.PUSHINT256:
                        sb.Append($" - {new BigInteger(instruction.Operand.Span)}");
                        break;
                    case OpCode.PUSHA:
                        sb.Append($" - {instruction.TokenI32}");
                        break;
                    case OpCode.PUSHDATA1:
                    case OpCode.PUSHDATA2:
                    case OpCode.PUSHDATA4:
                        sb.Append($" - {instruction.Operand.Span.ToHexString()}");
                        break;
                    case OpCode.JMP:
                    case OpCode.JMPIF:
                    case OpCode.JMPIFNOT:
                    case OpCode.JMPEQ:
                    case OpCode.JMPNE:
                    case OpCode.JMPGT:
                    case OpCode.JMPGE:
                    case OpCode.JMPLT:
                    case OpCode.JMPLE:
                    case OpCode.CALL:
                    case OpCode.ENDTRY:
                        sb.Append($" - offset:{instruction.TokenI8}=>[{offset + instruction.TokenI8}]");
                        break;
                    case OpCode.JMP_L:
                    case OpCode.JMPIF_L:
                    case OpCode.JMPIFNOT_L:
                    case OpCode.JMPEQ_L:
                    case OpCode.JMPNE_L:
                    case OpCode.JMPGT_L:
                    case OpCode.JMPGE_L:
                    case OpCode.JMPLT_L:
                    case OpCode.JMPLE_L:
                    case OpCode.CALL_L:
                    case OpCode.ENDTRY_L:
                        sb.Append($" - offset:{instruction.TokenI32}=>[{offset + instruction.TokenI32}]");
                        break;
                    case OpCode.CALLT:
                        sb.Append($" - {instruction.TokenU16}");
                        break;
                    case OpCode.TRY:
                        sb.Append($" - catch:{instruction.TokenI8}=>[{offset + instruction.TokenI8}],final:{instruction.TokenI8_1}=>[{offset + instruction.TokenI8_1}]");
                        break;
                    case OpCode.TRY_L:
                        sb.Append($" - catch:{instruction.TokenI32}=>[{offset + instruction.TokenI32}],final:{instruction.TokenI32_1}=>[{offset + instruction.TokenI32_1}]");
                        break;
                    case OpCode.SYSCALL:
                        if (ApplicationEngine.Services.TryGetValue(instruction.TokenU32, out var method))
                        {
                            sb.Append($" - {method.Name}");
                        }
                        break;
                    case OpCode.INITSLOT:
                        sb.Append($" - local:{instruction.TokenU8}, args:{instruction.TokenU8_1}");
                        break;
                    case OpCode.INITSSLOT:
                    case OpCode.LDSFLD:
                    case OpCode.STSFLD:
                    case OpCode.LDLOC:
                    case OpCode.STLOC:
                    case OpCode.LDARG:
                    case OpCode.STARG:
                    case OpCode.NEWARRAY_T:
                    case OpCode.CONVERT:
                        sb.Append($" - {instruction.TokenU8}");
                        break;
                }
                list.Add(sb.ToString());
                sb.Clear();
            }
            return list;
        }
    }
}
