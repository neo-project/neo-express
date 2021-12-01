using System;
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

        [Argument(0, Description = "name of " + EXPRESS_EXTENSION + " file to create (Default: ./" + DEFAULT_EXPRESS_FILENAME + ")")]
        internal string Output { get; set; } = string.Empty;

        [Option(Description = "Number of consensus nodes to create\nDefault: 1")]
        [AllowedValues("1", "4", "7")]
        internal int Count { get; set; } = 1;

        [Option(Description = "Version to use for addresses in this blockchain instance\nDefault: 53")]
        internal byte? AddressVersion { get; set; }

        [Option(Description = "Overwrite existing data")]
        internal bool Force { get; set; }

        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chainManager, outputPath) = chainManagerFactory.CreateChain(Count, AddressVersion, Output, Force);
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
