// Copyright (C) 2015-2024 The Neo Project.
//
// ContractCommand.Validate.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
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
        [Command("nep11", Description = "Checks if contract is NEP-11 compliant")]
        public class Nep11Compliant
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Nep11Compliant(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Path to contract .nef file")]
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

                    if (nep11)
                        await console.Out.WriteLineAsync($"{scriptHash} is NEP-11 compliant.");

                    return 0;
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

            [Argument(0, Description = "Path to contract .nef file")]
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

                    if (nep17)
                        await console.Out.WriteLineAsync($"{scriptHash} is NEP-17 compliant.");

                    return 0;
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
