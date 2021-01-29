using System;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("create", Description = "Create new neo-express instance")]
    internal class CreateCommand
    {
        readonly IExpressChainManagerFactory chainManagerFactory;

        public CreateCommand(IExpressChainManagerFactory chainManagerFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
        }

        [Argument(0, Description = "name of .neo-express file to create (Default: ./default.neo-express")]
        internal string Output { get; set; } = string.Empty;

        [Option(Description = "Number of consensus nodes to create\nDefault: 1")]
        [AllowedValues("1", "4", "7")]
        internal int Count { get; set; } = 1;

        [Option(Description = "Overwrite existing data")]
        internal bool Force { get; set; }

        internal int OnExecute(IConsole console)
        {
            try
            {
                var (chainManager, outputPath) = chainManagerFactory.CreateChain(Count, Output, Force);
                chainManager.SaveChain(outputPath);

                console.Out.WriteLine($"Created {Count} node privatenet at {outputPath}");
                console.Out.WriteLine("    Note: The private keys for the accounts in this file are are *not* encrypted.");
                console.Out.WriteLine("          Do not use these accounts on MainNet or in any other system where security is a concern.");

                return 0;
            }
            catch (Exception ex)
            {
                console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}
