// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using McMaster.Extensions.CommandLineUtils;
using System;
using System.IO;
using System.Threading.Tasks;

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
