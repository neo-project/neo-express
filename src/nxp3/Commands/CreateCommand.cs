using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;

namespace nxp3.Commands
{
    [Command("create")]
    class CreateCommand
    {
        [Argument(0, Description = "name of .neo-express file to create (Default: ./default.neo-express")]
        string Output { get; } = string.Empty;


        [Option(Description = "Number of consensus nodes to create\nDefault: 1")]
        [AllowedValues("1", "4", "7")]
        int Count { get; } = 1;

        [Option]
        bool Force { get; }

        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                var output = Program.GetDefaultFilename(Output);
                if (File.Exists(output))
                {
                    if (Force)
                    {
                        File.Delete(output);
                    }
                    else
                    {
                        throw new Exception("You must specify --force to overwrite an existing file");
                    }
                }

                var blockchainOperations = new BlockchainOperations();
                var chain = blockchainOperations.CreateBlockchain(new FileInfo(output), Count, Console.Out);
                chain.Save(output);

                return 0;
            }
            catch (Exception ex)
            {
                console.WriteLine(ex.Message);
                app.ShowHelp();
                return 1;
            }
        }
    }
}
