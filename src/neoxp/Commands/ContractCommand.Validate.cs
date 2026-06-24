// Copyright (C) 2015-2026 The Neo Project.
//
// ContractCommand.Validate.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands;

internal partial class ContractCommand
{
    [Command("validate", Description = "Checks a contract for compliance with proposal specification")]
    [Subcommand(
        typeof(Nep17Compliant),
        typeof(Nep11Compliant))]
    internal class Validate
    {
        // A validate command must report non-compliance and exit non-zero, so a
        // script (e.g. CI) does not read an empty stdout with exit 0 as success.
        internal static (string message, int exitCode) ComplianceResult(UInt160 scriptHash, string standard, bool compliant)
            => compliant
                ? ($"{scriptHash} is {standard} compliant.", 0)
                : ($"{scriptHash} is NOT {standard} compliant.", 1);

        internal static Task WriteMessageAsync(IConsole console, string message)
            => string.IsNullOrWhiteSpace(message)
                ? Task.CompletedTask
                : console.Out.WriteLineAsync(message);

        [Command("nep11", Description = "Checks if contract is NEP-11 compliant")]
        public class Nep11Compliant
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Nep11Compliant(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Contract script hash")]
            [Required]
            internal string ContractHash { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    if (UInt160.TryParse(ContractHash, out var scriptHash) == false)
                        throw new Exception($"{ContractHash} is invalid ScriptHash.");

                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();
                    var nep11 = await expressNode.IsNep11CompliantAsync(scriptHash).ConfigureAwait(false);

                    var (message, exitCode) = ComplianceResult(scriptHash, "NEP-11", nep11);
                    await WriteMessageAsync(console, message);
                    return exitCode;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex?.InnerException! ?? ex!);
                    return 1;
                }
            }
        }

        [Command("nep17", Description = "Checks if contract is NEP-17 compliant")]
        public class Nep17Compliant
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Nep17Compliant(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Contract script hash")]
            [Required]
            internal string ContractHash { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    if (UInt160.TryParse(ContractHash, out var scriptHash) == false)
                        throw new Exception($"{ContractHash} is invalid ScriptHash.");

                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();
                    var nep17 = await expressNode.IsNep17CompliantAsync(scriptHash).ConfigureAwait(false);

                    var (message, exitCode) = ComplianceResult(scriptHash, "NEP-17", nep17);
                    await WriteMessageAsync(console, message);
                    return exitCode;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex?.InnerException! ?? ex!);
                    return 1;
                }
            }
        }
    }
}
