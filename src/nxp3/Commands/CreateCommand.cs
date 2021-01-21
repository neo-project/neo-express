using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;


namespace NeoExpress.Commands
{
    [Command("create", Description = "Create new neo-express instance")]
    class CreateCommand
    {
        [Argument(0, Description = "name of .neo-express file to create (Default: ./default.neo-express")]
        string Output { get; } = string.Empty;


        [Option(Description = "Number of consensus nodes to create\nDefault: 1")]
        [AllowedValues("1", "4", "7")]
        int Count { get; } = 1;

        [Option(Description = "Overwrite existing data")]
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
                console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}
