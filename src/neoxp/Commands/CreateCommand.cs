// Copyright (C) 2015-2023 The Neo Project.
//
// CreateCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using static Neo.BlockchainToolkit.Constants;

namespace NeoExpress.Commands
{
    [Command("create", Description = "Create new neo-express instance")]
    internal class CreateCommand
    {
        readonly ExpressChainManagerFactory chainManagerFactory;

        public CreateCommand(ExpressChainManagerFactory chainManagerFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
        }

        [Argument(0, Description = $"Name of {EXPRESS_EXTENSION} file to create\nDefault location is home directory as:\nLinux: $HOME/.neo-express/{DEFAULT_EXPRESS_FILENAME}\nWindows: %UserProfile%\\.neo-express\\{DEFAULT_EXPRESS_FILENAME}")]
        internal string Output { get; set; } = string.Empty;

        [Option(Description = "Number of consensus nodes to create (Default: 1)")]
        [AllowedValues("1", "4", "7")]
        internal int Count { get; set; } = 1;

        [Option(Description = "Version to use for addresses in this blockchain instance (Default: 53)")]
        internal byte? AddressVersion { get; set; }

        [Option(Description = "Overwrite existing data")]
        internal bool Force { get; set; }

        [Option(Description = "Private key for default dev account (Default: Random)")]
        internal string PrivateKey { get; set; } = string.Empty;

        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                byte[]? priKey = null;
                if (string.IsNullOrEmpty(PrivateKey) == false)
                    priKey = Convert.FromHexString(PrivateKey);

                var (chainManager, outputPath) = chainManagerFactory.CreateChain(Count, AddressVersion, Output, Force, privateKey: priKey);
                chainManager.SaveChain(outputPath);

                console.Out.WriteLine($"Created {Count} node privatenet at {outputPath}");
                console.Out.WriteLine("    Note: The private keys for the accounts in this file are are *not* encrypted.");
                console.Out.WriteLine("          Do not use these accounts on MainNet or in any other system where security is a concern.");

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
