// Copyright (C) 2015-2024 The Neo Project.
//
// RunCommand.cs file belongs to neo-express project and is free
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
    [Command("run", Description = "Run Neo-Express instance node")]
    class RunCommand
    {
        readonly ExpressChainManagerFactory chainManagerFactory;

        public RunCommand(ExpressChainManagerFactory chainManagerFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
        }

        [Option(Description = "Index of node to run")]
        internal int NodeIndex { get; init; } = 0;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        [Option(Description = "Time between blocks")]
        internal uint? SecondsPerBlock { get; }

        [Option(Description = "Discard blockchain changes on shutdown")]
        internal bool Discard { get; init; } = false;

        [Option(Description = "Enable contract execution tracing")]
        internal bool Trace { get; init; } = false;

        internal async Task ExecuteAsync(IConsole console, CancellationToken token)
        {
            var (chainManager, _) = chainManagerFactory.LoadChain(Input, SecondsPerBlock);
            var chain = chainManager.Chain;

            if (NodeIndex < 0 || NodeIndex >= chain.ConsensusNodes.Count)
                throw new Exception("Invalid node index");

            var node = chain.ConsensusNodes[NodeIndex];
            var storageProvider = chainManager.GetNodeStorageProvider(node, Discard);
            using var disposable = storageProvider as IDisposable ?? Nito.Disposables.NoopDisposable.Instance;
            await chainManager.RunAsync(storageProvider, node, Trace, console, token);
        }

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
        {
            try
            {
                await ExecuteAsync(console, token);
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
