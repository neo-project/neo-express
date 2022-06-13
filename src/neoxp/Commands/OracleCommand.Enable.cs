using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using TextWriter = System.IO.TextWriter;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("enable", Description = "Enable oracles for neo-express instance")]
        internal class Enable
        {
            readonly IExpressChain chain;

            public Enable(IExpressChain chain)
            {
                this.chain = chain;
            }

            public Enable(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Account to pay contract invocation GAS fee")]
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
                var txHash = await ExecuteAsync(expressNode, Account, password).ConfigureAwait(false);
                console.Out.WriteTxHash(txHash, "Oracle Enable", Json);
            }

            public static async Task<UInt256> ExecuteAsync(IExpressNode expressNode, string account, string password, TextWriter? writer = null)
            {
                var (wallet, accountHash) = expressNode.Chain.ResolveSigner(account, password);

                var oracles = expressNode.Chain.ConsensusNodes
                    .Select(n => n.Wallet.DefaultAccount ?? throw new Exception("missing default account"))
                    .Select(a => new KeyPair(Convert.FromHexString(a.PrivateKey)))
                    .Select(kp => new ContractParameter(ContractParameterType.PublicKey) { Value = kp.PublicKey });

                var roleParam = new ContractParameter(ContractParameterType.Integer) { Value = (BigInteger)(byte)Role.Oracle };
                var oraclesParam = new ContractParameter(ContractParameterType.Array) { Value = oracles.ToList() };

                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(NativeContract.RoleManagement.Hash, "designateAsRole", roleParam, oraclesParam);
                var txHash = await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, builder.ToArray()).ConfigureAwait(false);
                writer?.WriteTxHash(txHash, $"Oracle Enable");
                return txHash;
            }
        }
    }
}
