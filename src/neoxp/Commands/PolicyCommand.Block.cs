using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Native;
using Neo.VM;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command(Name = "block", Description = "Block account from usage")]
        internal class Block
        {
            readonly IExpressChain chain;

            public Block(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Block(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Account to block")]
            [Required]
            internal string ScriptHash { get; init; } = string.Empty;

            [Argument(1, Description = "Account to pay contract invocation GAS fee")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "password to use for NEP-2/NEP-6 sender")]
            internal string Password { get; init; } = string.Empty;

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal Task<int> OnExecuteAsync(CommandLineApplication app)
                => app.ExecuteAsync(this.ExecuteAsync);

            internal async Task ExecuteAsync(IConsole console)
            {
                using var expressNode = chain.GetExpressNode(Trace);
                var password = chain.ResolvePassword(Account, Password);
                var txHash = await ExecuteAsync(expressNode, ScriptHash, Account, password).ConfigureAwait(false);
                console.Out.WriteTxHash(txHash, $"{ScriptHash} blocked", Json);
            }

            public static async Task<UInt256> ExecuteAsync(IExpressNode expressNode, string scriptHash, string account, string password)
            {
                var (wallet, accountHash) = expressNode.Chain.ResolveSigner(account, password);
                var hash = await PolicyCommand.ResolveScriptHashAsync(expressNode, scriptHash);
                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(NativeContract.Policy.Hash, "blockAccount", hash);
                return await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, builder.ToArray()).ConfigureAwait(false);
            }
        }
    }
}
