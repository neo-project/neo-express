using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("list", Description = "List oracle nodes")]
        internal class List
        {
            readonly IExpressChain chain;

            public List(IExpressChain chain)
            {
                this.chain = chain;
            }

            public List(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode();
                var oracleNodes = await ListOracleNodesAsync(expressNode);

                await console.Out.WriteLineAsync($"Oracle Nodes ({oracleNodes.Count}): ").ConfigureAwait(false);
                for (var x = 0; x < oracleNodes.Count; x++)
                {
                    await console.Out.WriteLineAsync($"  {oracleNodes[x]}").ConfigureAwait(false);
                }
            }

            public static async Task<IReadOnlyList<ECPoint>> ListOracleNodesAsync(IExpressNode expressNode)
            {
                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(NativeContract.Ledger.Hash, "currentIndex");
                builder.Emit(OpCode.INC);
                builder.EmitPush((byte)Role.Oracle);
                builder.EmitPush(2);
                builder.Emit(OpCode.PACK);
                builder.EmitPush(CallFlags.ReadOnly);
                builder.EmitPush("getDesignatedByRole");
                builder.EmitPush(NativeContract.RoleManagement.Hash);
                builder.EmitSysCall(ApplicationEngine.System_Contract_Call);

                var result = await expressNode.GetResultAsync(builder.ToArray()).ConfigureAwait(false);
                var array = (Neo.VM.Types.Array)result.Stack[0];

                return array.Select(i => ECPoint.DecodePoint(i.GetSpan(), ECCurve.Secp256r1)).ToList();
            }
        }
    }
}
