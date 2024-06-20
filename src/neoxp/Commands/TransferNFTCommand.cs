// Copyright (C) 2015-2024 The Neo Project.
//
// TransferNFTCommand.cs file belongs to neo-express project and is free
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
using System.Text;
using System.Text.RegularExpressions;

namespace NeoExpress.Commands
{
    [Command("transfernft", Description = "Transfer NFT asset between accounts")]
    class TransferNFTCommand
    {
        readonly ExpressChainManagerFactory chainManagerFactory;
        readonly TransactionExecutorFactory txExecutorFactory;

        public TransferNFTCommand(ExpressChainManagerFactory chainManagerFactory, TransactionExecutorFactory txExecutorFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
            this.txExecutorFactory = txExecutorFactory;
        }

        [Argument(0, Description = "NFT Contract (Symbol or Script Hash)")]
        [Required]
        internal string Contract { get; init; } = string.Empty;

        [Argument(1, Description = "TokenId of NFT (Format: HEX, BASE64)")]
        [Required]
        internal string TokenId { get; init; } = string.Empty;

        [Argument(2, Description = "Account to send NFT from (Format: Wallet name, WIF)")]
        [Required]
        internal string Sender { get; init; } = string.Empty;

        [Argument(3, Description = "Account to send NFT to (Format: Script Hash, Address, Wallet name)")]
        [Required]
        internal string Receiver { get; init; } = string.Empty;

        [Option(Description = "Optional data parameter to pass to transfer operation")]
        internal string Data { get; init; } = string.Empty;

        [Option(Description = "password to use for NEP-2/NEP-6 sender")]
        internal string Password { get; init; } = string.Empty;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        [Option(Description = "Enable contract execution tracing")]
        internal bool Trace { get; init; } = false;

        [Option(Description = "Output as JSON")]
        internal bool Json { get; init; } = false;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                var password = chainManager.Chain.ResolvePassword(Sender, Password);
                using var txExec = txExecutorFactory.Create(chainManager, Trace, Json);
                await txExec.TransferNFTAsync(Contract, HexOrBase64ToUTF8(TokenId), Sender, password, Receiver, Data).ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex)
            {
                app.WriteException(ex);
                return 1;
            }

            static string HexOrBase64ToUTF8(string input)
            {
                try
                {
                    return input.StartsWith("0x") ? Encoding.UTF8.GetString(input[2..].HexToBytes().Reverse().ToArray()) : Encoding.UTF8.GetString(Convert.FromBase64String(input));
                }
                catch (Exception)
                {
                    throw new ArgumentException($"Unknown Asset \"{input}\"", nameof(TokenId));
                }
            }
        }
    }
}
