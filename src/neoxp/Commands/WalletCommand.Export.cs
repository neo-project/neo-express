// Copyright (C) 2015-2026 The Neo Project.
//
// WalletCommand.Export.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
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
            internal bool Force { get; init; }

            [Option(Description = "Password to use for the exported NEP-6 wallet (prompted for if unspecified)")]
            internal string Password { get; init; } = string.Empty;

            internal string Execute()
                => Execute(Console.IsInputRedirected,
                    () => Prompt.GetPassword("Input password to use for exported wallet"));

            internal string Execute(bool isInputRedirected, Func<string> promptForPassword)
            {
                var output = string.IsNullOrEmpty(Output)
                   ? fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), $"{Name}.wallet.json")
                   : fileSystem.Path.GetFullPath(Output);

                var (chainManager, chainPath) = chainManagerFactory.LoadChain(Input);
                var wallet = chainManager.Chain.GetWallet(Name);

                if (wallet is null)
                {
                    throw new Exception($"{Name} express wallet not found.");
                }

                if (fileSystem.File.Exists(output))
                {
                    if (!Force)
                    {
                        throw new Exception("You must specify force to overwrite an exported wallet.");
                    }
                }

                var password = Extensions.ResolveExportPassword(Password, isInputRedirected, promptForPassword);
                var devWallet = DevWallet.FromExpressWallet(chainManager.ProtocolSettings, wallet);
                var directory = fileSystem.Path.GetDirectoryName(output)
                    ?? fileSystem.Directory.GetCurrentDirectory();
                var temporaryOutput = fileSystem.Path.Combine(
                    directory,
                    $".{fileSystem.Path.GetFileName(output)}.{Guid.NewGuid():N}.tmp");
                try
                {
                    devWallet.Export(temporaryOutput, password);
                    fileSystem.File.Move(temporaryOutput, output, Force);
                    return output;
                }
                finally
                {
                    if (fileSystem.File.Exists(temporaryOutput))
                    {
                        fileSystem.File.Delete(temporaryOutput);
                    }
                }
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
