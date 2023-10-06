// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using McMaster.Extensions.CommandLineUtils;
using System.IO.Abstractions;

namespace NeoExpress
{
    class TransactionExecutorFactory
    {
        readonly IFileSystem fileSystem;
        readonly IConsole console;

        public TransactionExecutorFactory(IFileSystem fileSystem, IConsole console)
        {
            this.fileSystem = fileSystem;
            this.console = console;
        }

        public TransactionExecutor Create(ExpressChainManager chainManager, bool trace, bool json)
        {
            return new TransactionExecutor(fileSystem, chainManager, trace, json, console.Out);
        }
    }
}
