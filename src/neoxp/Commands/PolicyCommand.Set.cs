using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command(Name = "set", Description = "Set single policy value")]
        internal class Set
        {
            readonly IExpressChain expressFile;

            public Set(IExpressChain expressFile)
            {
                this.expressFile = expressFile;
            }

            public Set(CommandLineApplication app) : this(app.GetExpressFile())
            {
            }

            [Argument(0, Description = "Policy to set")]
            [Required]
            internal PolicySettings Policy { get; init; }

            [Argument(1, Description = "New Policy Value")]
            [Required]
            internal decimal Value { get; set; }

            [Argument(2, Description = "Account to pay contract invocation GAS fee")]
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
                var password = expressFile.ResolvePassword(Account, Password);
                using var expressNode = expressFile.GetExpressNode(Trace);
                var txHash = await ExecuteAsync(expressNode, Policy, Value, Account, password).ConfigureAwait(false);
                await console.Out.WriteTxHashAsync(txHash, $"{Policy} Policy Set", Json).ConfigureAwait(false);
            }

            public static async Task<UInt256> ExecuteAsync(IExpressNode expressNode, PolicySettings policy, decimal value, string account, string password)
            {
                var (wallet, accountHash) = expressNode.ExpressChain.ResolveSigner(account, password);

                var (hash, operation) = policy switch
                {
                    PolicySettings.GasPerBlock => (NativeContract.NEO.Hash, "setGasPerBlock"),
                    PolicySettings.MinimumDeploymentFee => (NativeContract.ContractManagement.Hash, "setMinimumDeploymentFee"),
                    PolicySettings.CandidateRegistrationFee => (NativeContract.NEO.Hash, "setRegisterPrice"),
                    PolicySettings.OracleRequestFee => (NativeContract.Oracle.Hash, "setPrice"),
                    PolicySettings.NetworkFeePerByte => (NativeContract.Policy.Hash, "setFeePerByte"),
                    PolicySettings.StorageFeeFactor => (NativeContract.Policy.Hash, "setStoragePrice"),
                    PolicySettings.ExecutionFeeFactor => (NativeContract.Policy.Hash, "setExecFeeFactor"),
                    _ => throw new InvalidOperationException($"Unknown policy {policy}"),
                };

                // Calculate decimal count : https://stackoverflow.com/a/13493771/1179731
                int decimalCount = BitConverter.GetBytes(decimal.GetBits(value)[3])[2];
                var decimalValue = new BigDecimal(value, (byte)decimalCount);
                using var builder = new ScriptBuilder();
                if (GasPolicySetting(policy))
                {
                    if (decimalValue.Decimals > NativeContract.GAS.Decimals)
                        throw new InvalidOperationException($"{policy} policy requires a value with no more than eight decimal places");
                    decimalValue = decimalValue.ChangeDecimals(NativeContract.GAS.Decimals);
                    builder.EmitDynamicCall(hash, operation, decimalValue.Value);
                }
                else
                {
                    if (decimalCount != 0)
                        throw new InvalidOperationException($"{policy} policy requires a whole number value");
                    if (decimalValue.Value > uint.MaxValue)
                        throw new InvalidOperationException($"{policy} policy requires a value less than {uint.MaxValue}");
                    builder.EmitDynamicCall(hash, operation, (uint)decimalValue.Value);
                }

                return await expressNode.ExecuteAsync(wallet, accountHash, WitnessScope.CalledByEntry, builder.ToArray()).ConfigureAwait(false);

                static bool GasPolicySetting(PolicySettings policy)
                {
                    switch (policy)
                    {
                        case PolicySettings.GasPerBlock:
                        case PolicySettings.MinimumDeploymentFee:
                        case PolicySettings.CandidateRegistrationFee:
                        case PolicySettings.OracleRequestFee:
                        case PolicySettings.NetworkFeePerByte:
                            return true;
                        default:
                            return false;
                    }
                }
            }
        }
    }
}
