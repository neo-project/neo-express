// Copyright (C) 2015-2026 The Neo Project.
//
// TransactionCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using System.ComponentModel.DataAnnotations;
using static Neo.BlockchainToolkit.Utility;

namespace NeoTrace.Commands
{
    [Command("transaction", "tx", Description = "Trace the specified transaction")]
    class TransactionCommand
    {
        [Argument(0, Description = "Transaction hash")]
        [Required]
        internal string TransactionHash { get; } = string.Empty;

        [Option(Description = "URL of Neo JSON-RPC Node\nSpecify MainNet (default), TestNet or JSON-RPC URL")]
        internal string RpcUri { get; } = string.Empty;

        [Option("--timeout <SECONDS>", Description = "Maximum tracing time in seconds. Use 0 to disable the timeout.")]
        internal int Timeout { get; } = Program.DefaultTimeoutSeconds;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
        {
            try
            {
                if (!TryParseRpcUri(RpcUri, out var uri))
                {
                    throw new ArgumentException($"Invalid RpcUri value \"{RpcUri}\"");
                }
                var txHash = UInt256.TryParse(TransactionHash, out var _txHash)
                    ? _txHash
                    : throw new ArgumentException($"Invalid transaction hash {TransactionHash}");
                await Program.RunWithTimeoutAsync(
                    traceToken => Program.TraceTransactionAsync(uri, txHash, console, traceToken),
                    Timeout,
                    token).ConfigureAwait(false);
                return 0;
            }
            catch (TimeoutException ex)
            {
                await app.Error.WriteLineAsync(ex.Message);
                return 1;
            }
            catch (OperationCanceledException)
            {
                await app.Error.WriteLineAsync("Operation canceled");
                return 1;
            }
            catch (Exception ex)
            {
                await app.Error.WriteLineAsync(ex.Message);
                return 1;
            }
        }
    }
}
