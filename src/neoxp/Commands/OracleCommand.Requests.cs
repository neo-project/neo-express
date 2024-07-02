// Copyright (C) 2015-2024 The Neo Project.
//
// OracleCommand.Requests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class OracleCommand
    {
        [Command("requests", Description = "List outstanding oracle requests")]
        internal class Requests
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Requests(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task ExecuteAsync(TextWriter writer)
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                using var expressNode = chainManager.GetExpressNode();
                var requests = await expressNode.ListOracleRequestsAsync().ConfigureAwait(false);

                foreach (var (id, request) in requests)
                {
                    await writer.WriteLineAsync($"request #{id}:").ConfigureAwait(false);
                    await writer.WriteLineAsync($"    Original Tx Hash: {request.OriginalTxid}").ConfigureAwait(false);
                    await writer.WriteLineAsync($"    Request Url:      \"{request.Url}\"").ConfigureAwait(false);
                }
            }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    await ExecuteAsync(console.Out);
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }
        }
    }
}
