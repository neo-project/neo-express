// Copyright (C) 2023 neo-project
//
// The neo-examples-csharp is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using McMaster.Extensions.CommandLineUtils;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    partial class CheckpointCommand
    {
        [Command("create", Description = "Create a new neo-express checkpoint")]
        internal class Create
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Create(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, "Checkpoint file name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    using var expressNode = chainManager.GetExpressNode();
                    _ = await chainManager.CreateCheckpointAsync(expressNode, Name, Force, console.Out).ConfigureAwait(false);
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
