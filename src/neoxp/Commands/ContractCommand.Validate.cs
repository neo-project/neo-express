// Copyright (C) 2015-2023 The Neo Project.
//
// The neo is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Network.P2P.Payloads;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Security.Principal;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace NeoExpress.Commands;

internal partial class ContractCommand
{
    [Command("validate", Description = "Checks a contract for compliance with proposal specification")]
    internal class Validate
    {
        readonly ExpressChainManagerFactory chainManagerFactory;

        public Validate(ExpressChainManagerFactory chainManagerFactory)
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
                app.WriteException(ex, showInnerExceptions: true);
                return 1;
            }
        }
    }
}
