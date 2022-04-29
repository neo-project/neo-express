using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.VM;
using OneOf;

namespace NeoExpress.Commands
{
    [Command("transfer", Description = "Transfer asset between accounts")]
    class TransferCommand
    {
        readonly IExpressFile expressFile;

        public TransferCommand(IExpressFile expressFile)
        {
            this.expressFile = expressFile;
        }

        public TransferCommand(CommandLineApplication app) : this(app.GetExpressFile())
        {
        }

        [Argument(0, Description = "Amount to transfer")]
        [Required]
        internal string Quantity { get; init; } = string.Empty;

        [Argument(1, Description = "Asset to transfer (symbol or script hash)")]
        [Required]
        internal string Asset { get; init; } = string.Empty;

        [Argument(2, Description = "Account to send asset from")]
        [Required]
        internal string Sender { get; init; } = string.Empty;

        [Argument(3, Description = "Account to send asset to")]
        [Required]
        internal string Receiver { get; init; } = string.Empty;

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
            using var expressNode = expressFile.GetExpressNode(Trace);
            var password = expressFile.ResolvePassword(Sender, Password);
            var txHash = await TransferAsync(expressNode, Quantity, Asset, Sender, password, Receiver)
                .ConfigureAwait(false);
            await console.Out.WriteTxHashAsync(txHash, "Transfer", Json)
                .ConfigureAwait(false);
        }

        public static async Task<Neo.UInt256> TransferAsync(IExpressNode expressNode,
            
            string quantity, string asset, string sender, string password, string receiver)
        {
            var (senderWallet, senderHash) = expressNode.ExpressFile.ResolveSigner(sender, password);
            var receiverHash = expressNode.Chain.ResolveAccountHash(receiver);

            var assetHash = await expressNode.ParseAssetAsync(asset).ConfigureAwait(false);
            using var builder = new ScriptBuilder();
            if ("all".Equals(quantity, StringComparison.OrdinalIgnoreCase))
            {
                // balanceOf operation places current balance on eval stack
                builder.EmitDynamicCall(assetHash, "balanceOf", senderHash);
                // transfer operation takes 4 arguments, amount is 3rd parameter
                // push null onto the stack and then switch positions of the top
                // two items on eval stack so null is 4th arg and balance is 3rd
                builder.Emit(OpCode.PUSHNULL);
                builder.Emit(OpCode.SWAP);
                builder.EmitPush(receiverHash);
                builder.EmitPush(senderHash);
                builder.EmitPush(4);
                builder.Emit(OpCode.PACK);
                builder.EmitPush("transfer");
                builder.EmitPush(asset);
                builder.EmitSysCall(Neo.SmartContract.ApplicationEngine.System_Contract_Call);
            }
            else if (decimal.TryParse(quantity, out var amount))
            {
                var results = await expressNode.InvokeAsync(assetHash.MakeScript("decimals"))
                    .ConfigureAwait(false);
                if (results.Stack.Length == 0 || results.Stack[0].Type != Neo.VM.Types.StackItemType.Integer)
                {
                    throw new Exception();
                }
                var decimals = (byte)(results.Stack[0].GetInteger());
                builder.EmitDynamicCall(assetHash, "transfer", senderHash, receiverHash, amount.ToBigInteger(decimals), null);
            }
            else
            {
                throw new ArgumentException($"Invalid quantity value {quantity}");
            }

            return await expressNode.ExecuteAsync(senderWallet, senderHash, WitnessScope.CalledByEntry, builder.ToArray())
                .ConfigureAwait(false);
        }
    }
}
