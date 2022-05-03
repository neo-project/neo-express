using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Newtonsoft.Json;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("block", Description = "Show block")]
        internal class Block
        {
            readonly IExpressFile expressFile;

            public Block(IExpressFile expressFile)
            {
                this.expressFile = expressFile;
            }

            public Block(CommandLineApplication app) : this(app.GetExpressFile())
            {
            }

            [Argument(0, Description = "Optional block hash or index. Show most recent block if unspecified")]
            internal string BlockHash { get; init; } = string.Empty;

            internal Task<int> OnExecuteAsync(CommandLineApplication app) => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var builder = new ScriptBuilder();
                builder.Emit(OpCode.INITSLOT, new byte[] { 0x03, 0x00 });

                if (UInt256.TryParse(BlockHash, out var hash))
                {
                    var param = new ContractParameter(ContractParameterType.Hash256) { Value = hash };
                    builder.EmitDynamicCall(NativeContract.Ledger.Hash, "getBlock", param);
                }
                else if (uint.TryParse(BlockHash, out var index))
                {
                    var param = new ContractParameter(ContractParameterType.Integer) { Value = index };
                    builder.EmitDynamicCall(NativeContract.Ledger.Hash, "getBlock", param);
                }
                else if (string.IsNullOrEmpty(BlockHash))
                {
                    builder.EmitDynamicCall(NativeContract.Ledger.Hash, "currentIndex");

                    builder.EmitPush(1);
                    builder.Emit(OpCode.PACK);
                    builder.EmitPush(CallFlags.ReadOnly);
                    builder.EmitPush("getBlock");
                    builder.EmitPush(NativeContract.Ledger.Hash);
                    builder.EmitSysCall(ApplicationEngine.System_Contract_Call);
                }
                else
                {
                    throw new ArgumentException($"{nameof(BlockHash)} must be block index, block hash or empty");
                }

                // store the block in slot 0
                builder.Emit(OpCode.STLOC0);

                // get the tx count from block
                builder.Emit(OpCode.LDLOC0, OpCode.PUSH9, OpCode.PICKITEM);
                
                // create array to hold block transactions and store in loc 1
                builder.Emit(OpCode.NEWARRAY, OpCode.STLOC1);

                // store index variable in loc 2
                builder.Emit(OpCode.PUSH0, OpCode.STLOC2);

                // check if index less than tx count
                builder.Emit(OpCode.LDLOC2, OpCode.LDLOC0, OpCode.PUSH9, OpCode.PICKITEM, OpCode.LT);
                builder.Emit(OpCode.JMPIFNOT, new byte[] { unchecked((byte)checked((sbyte)70))  });

                // load index and block hash
                builder.Emit(OpCode.LDLOC2, OpCode.LDLOC0, OpCode.PUSH0, OpCode.PICKITEM);

                // invoke GetTransactionFromBlock
                builder.EmitPush(2);
                builder.Emit(OpCode.PACK);
                builder.EmitPush(CallFlags.ReadOnly);
                builder.EmitPush("getTransactionFromBlock");
                builder.EmitPush(NativeContract.Ledger.Hash);
                builder.EmitSysCall(ApplicationEngine.System_Contract_Call);

                // load array and index, move tx to top of stack and set array item value
                builder.Emit(OpCode.LDLOC1, OpCode.LDLOC2, OpCode.ROT, OpCode.SETITEM);

                // increment index
                builder.Emit(OpCode.LDLOC2, OpCode.INC, OpCode.STLOC2);
                
                // jump back to while loop start
                builder.Emit(OpCode.JMP, new byte[] { unchecked((byte)checked((sbyte)-73)) });

                // load block + tx array onto stack for return
                builder.Emit(OpCode.LDLOC0, OpCode.LDLOC1);

                using var expressNode = expressFile.GetExpressNode();
                var result = await expressNode.GetResultAsync(builder).ConfigureAwait(false);
                if (result.State != VMState.HALT) throw new Exception(result.Exception ?? string.Empty);
                
                var block = (Neo.VM.Types.Array)result.Stack[0];
                var txArray = (Neo.VM.Types.Array)result.Stack[1];

                using var writer = new JsonTextWriter(console.Out) { Formatting = Formatting.Indented };
                using var _ = writer.WriteObject();
                writer.WriteProperty("hash", $"{new UInt256(block[0].GetSpan())}");
                writer.WriteProperty("version", (uint)block[1].GetInteger());
                writer.WriteProperty("previousblockhash", $"{new UInt256(block[2].GetSpan())}");
                writer.WriteProperty("merkleroot", $"{new UInt256(block[3].GetSpan())}");
                writer.WriteProperty("time", (ulong)block[4].GetInteger());
                writer.WriteProperty("nonce", (ulong)block[5].GetInteger());
                writer.WriteProperty("index", (uint)block[6].GetInteger());
                writer.WriteProperty("primary", (byte)block[7].GetInteger());
                writer.WriteProperty("nextconsensus", new UInt160(block[8].GetSpan()).ToAddress(expressFile.Chain.AddressVersion));
                
                writer.WritePropertyName("transactions");
                using var __ = writer.WriteArray();
                foreach (var tx in txArray.Cast<Neo.VM.Types.Array>())
                {
                    using var _tx = writer.WriteObject();
                    writer.WriteProperty("hash", $"{new UInt256(tx[0].GetSpan())}");
                    writer.WriteProperty("version", (byte)tx[1].GetInteger());
                    writer.WriteProperty("nonce", (uint)tx[2].GetInteger());
                    writer.WriteProperty("sender", new UInt160(tx[3].GetSpan()).ToAddress(expressFile.Chain.AddressVersion));
                    writer.WriteProperty("sysfee", (long)tx[4].GetInteger());
                    writer.WriteProperty("netfee", (long)tx[5].GetInteger());
                    writer.WriteProperty("validuntilblock", (uint)tx[6].GetInteger());
                    writer.WriteProperty("script", Convert.ToBase64String(tx[7].GetSpan()));
                }
            }
        }
    }
}

  