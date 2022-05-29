using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
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

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("enable", Description = "Enable oracles for neo-express instance")]
        internal class Enable
        {
            readonly IExpressChain expressFile;

            public Enable(IExpressChain expressFile)
            {
                this.expressFile = expressFile;
            }

            public Enable(CommandLineApplication app) : this(app.GetExpressFile())
            {
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
                using var expressNode = expressFile.GetExpressNode(Trace);
                var password = expressFile.ResolvePassword(Account, Password);
                var txHash = await ExecuteAsync(expressNode, Account, password).ConfigureAwait(false);
                await console.Out.WriteTxHashAsync(txHash, "Oracle Enable", Json).ConfigureAwait(false);
            }

            public static async Task<UInt256> ExecuteAsync(IExpressNode expressNode, string account, string password)
            {
                var (wallet, accountHash) = expressNode.ExpressChain.ResolveSigner(account, password);

                var oracles = expressNode.Chain.ConsensusNodes
                    .Select(n => n.Wallet.DefaultAccount ?? throw new Exception("missing default account"))
                    .Select(a => new KeyPair(Convert.FromHexString(a.PrivateKey)))
                    .Select(kp => new ContractParameter(ContractParameterType.PublicKey) { Value = kp.PublicKey });

                var roleParam = new ContractParameter(ContractParameterType.Integer) { Value = (BigInteger)(byte)Role.Oracle };
                var oraclesParam = new ContractParameter(ContractParameterType.Array) { Value = oracles.ToList() };

                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(NativeContract.RoleManagement.Hash, "designateAsRole", roleParam, oraclesParam);
                return await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, builder.ToArray()).ConfigureAwait(false);
            }
        }
    }
}
