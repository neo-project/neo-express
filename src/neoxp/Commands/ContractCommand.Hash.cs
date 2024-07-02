// Copyright (C) 2015-2024 The Neo Project.
//
// ContractCommand.Hash.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using static Neo.SmartContract.Helper;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "hash", Description = "Get contract hash for contract path and deployment account")]
        internal class Hash
        {
            readonly ExpressChainManagerFactory chainManagerFactory;
            readonly IFileSystem fileSystem;

            public Hash(ExpressChainManagerFactory chainManagerFactory, IFileSystem fileSystem)
            {
                this.chainManagerFactory = chainManagerFactory;
                this.fileSystem = fileSystem;
            }

            [Argument(0, Description = "Path to contract .nef file")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Argument(1, Description = "Account that would deploy the contract")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    if (!chainManager.Chain.TryGetAccountHash(Account, out var accountHash))
                    {
                        throw new Exception($"{Account} account not found.");
                    }

                    var (nefFile, manifest) = await fileSystem.LoadContractAsync(Contract).ConfigureAwait(false);
                    var contractHash = GetContractHash(accountHash, nefFile.CheckSum, manifest.Name);

                    await console.Out.WriteLineAsync($"{contractHash}").ConfigureAwait(false);

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
