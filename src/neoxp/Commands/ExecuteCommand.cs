// Copyright (C) 2015-2023 The Neo Project.
//
// ExecuteCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo.Network.P2P.Payloads;
using Neo.VM;
using OneOf;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands
{
    [Command(Name = "execute", Description = "Invoke a script ")]

    class ExecuteCommand
    {

        readonly ExpressChainManagerFactory chainManagerFactory;
        readonly TransactionExecutorFactory txExecutorFactory;

        public ExecuteCommand(ExpressChainManagerFactory chainManagerFactory, TransactionExecutorFactory txExecutorFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
            this.txExecutorFactory = txExecutorFactory;
        }

        [Argument(0, Description = "A validate neo-vm script")]
        [Required]
        internal string InputScript { get; set; } = string.Empty;



        [Option("--format|-f", Description = "Input script format(hex,b64,file)")]
        [AllowedValues(StringComparison.OrdinalIgnoreCase, "hex", "b64", "file")]
        internal InputFormatType InputFormat { get; init; } = InputFormatType.Hex;

        [Option(Description = "Account to pay invocation GAS fee")]
        internal string Account { get; init; } = string.Empty;

        [Option(Description = "Witness Scope to use for transaction signer (Default: CalledByEntry)")]
        [AllowedValues(StringComparison.OrdinalIgnoreCase, "None", "CalledByEntry", "Global")]
        internal WitnessScope WitnessScope { get; init; } = WitnessScope.CalledByEntry;

        [Option(Description = "Invoke contract for results (does not cost GAS)")]
        internal bool Results { get; init; } = false;

        [Option("--gas|-g", CommandOptionType.SingleValue, Description = "Additional GAS to apply to the contract invocation")]
        internal decimal AdditionalGas { get; init; } = 0;

        [Option(Description = "password to use for NEP-2/NEP-6 account")]
        internal string Password { get; init; } = string.Empty;

        [Option(Description = "Enable contract execution tracing")]
        internal bool Trace { get; init; } = false;

        [Option(Description = "Output as JSON")]
        internal bool Json { get; init; } = false;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                if (string.IsNullOrEmpty(Account) && !Results)
                {
                    throw new Exception("Either Account or --results must be specified");
                }

                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                using var txExec = txExecutorFactory.Create(chainManager, Trace, Json);

                var result = TryConvertToScript();
                if (result.TryPickT1(out var msg, out var script))
                {
                    console.WriteLine(msg);
                    return 1;
                }

                if (Results)
                {
                    await txExec.InvokeForResultsAsync(script, Account, WitnessScope);
                }
                else
                {
                    var password = chainManager.Chain.ResolvePassword(Account, Password);
                    await txExec.ContractInvokeAsync(script, Account, password, WitnessScope, AdditionalGas);
                }

                return 0;
            }
            catch (Exception ex)
            {
                app.WriteException(ex, showInnerExceptions: true);
                return 1;
            }
        }


        private OneOf<Script, string> TryConvertToScript()
        {
            byte[] data = null;
            try
            {
                data = Convert.FromHexString(InputScript);
            }
            catch (Exception e)
            {
                return e.ToString();
            }

            try
            {
                var s = new Script(data, true);
                return s;
            }
            catch (Exception e)
            {
                return e.ToString();
            }

        }
    }

    public enum InputFormatType
    {
        Hex,
        B64,
        File
    }


}
