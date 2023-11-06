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
