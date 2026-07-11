// Copyright (C) 2015-2026 The Neo Project.
//
// BlockCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using OneOf;
using System.ComponentModel.DataAnnotations;
using static Neo.BlockchainToolkit.Utility;

namespace NeoTrace.Commands
{
    [Command("block", Description = "Trace all transactions in a specified block")]
    class BlockCommand
    {
        [Argument(0, Description = "Block index or hash")]
        [Required]
        internal string BlockIdentifier { get; } = string.Empty;

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
                var blockId = ParseBlockIdentifier();

                await Program.RunWithTimeoutAsync(
                    traceToken => Program.TraceBlockAsync(uri, blockId, console, traceToken),
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
            catch (TimeoutException ex)
            {
                await app.Error.WriteLineAsync(ex.Message);
            }
            catch (OperationCanceledException)
            {
                await app.Error.WriteLineAsync("Operation canceled");
            }
            catch (Exception ex)
            {
                 await app.Error.WriteLineAsync(ex.Message);
            }
            return  1;
        }

        OneOf<uint, UInt256> ParseBlockIdentifier()
        {
            if (uint.TryParse(BlockIdentifier, out var index))
            {
                if (index == 0)
                    throw new ArgumentException("Cannot trace genesis block");
                return index;
            }
            if (UInt256.TryParse(BlockIdentifier, out var hash))
                return hash;

            throw new ArgumentException($"Invalid Block Identifier {BlockIdentifier}");
        }
    }
}
