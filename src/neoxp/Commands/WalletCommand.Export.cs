// Copyright (C) 2015-2024 The Neo Project.
//
// WalletCommand.Export.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;

namespace NeoExpress.Commands
{
    partial class WalletCommand
    {
        [Command("export", Description = "Export neo-express wallet in NEP-6 format")]
        internal class Export
        {
            readonly ExpressChainManagerFactory chainManagerFactory;
            readonly IFileSystem fileSystem;

            public Export(ExpressChainManagerFactory chainManagerFactory, IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Wallet name")]
            [Required]
            internal string Name { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "NEP-6 wallet name (Defaults to Neo-Express name if unspecified)")]
            internal string Output { get; init; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }

            internal string Execute()
            {
                var output = string.IsNullOrEmpty(Output)
                   ? fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), $"{Name}.wallet.json")
                   : Output;

                var (chainManager, chainPath) = chainManagerFactory.LoadChain(Input);
                var wallet = chainManager.Chain.GetWallet(Name);

                if (wallet is null)
                {
                    throw new Exception($"{Name} express wallet not found.");
                }

                if (fileSystem.File.Exists(output))
                {
                    if (Force)
                    {
                        fileSystem.File.Delete(output);
                    }
                    else
                    {
                        throw new Exception("You must specify force to overwrite an exported wallet.");
                    }
                }

                var password = Prompt.GetPassword("Input password to use for exported wallet");
                var devWallet = DevWallet.FromExpressWallet(chainManager.ProtocolSettings, wallet);
                devWallet.Export(output, password);
                return output;
            }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var output = Execute();
                    console.WriteLine($"{Name} privatenet wallet exported to {output}");
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
