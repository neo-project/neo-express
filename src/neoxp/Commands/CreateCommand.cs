using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    [Command("create", Description = "Create new neo-express instance")]
    internal class CreateCommand
    {
        readonly IFileSystem fileSystem; 
        readonly IBlockchainOperations chainManger;

        public CreateCommand(IFileSystem fileSystem, IBlockchainOperations chainManger)
        {
            this.fileSystem = fileSystem;
            this.chainManger = chainManger;
        }

        [Argument(0, Description = "name of .neo-express file to create (Default: ./default.neo-express")]
        internal string Output { get; set; } = string.Empty;

        [Option(Description = "Number of consensus nodes to create\nDefault: 1")]
        [AllowedValues("1", "4", "7")]
        internal int Count { get; set; } = 1;

        [Option(Description = "Overwrite existing data")]
        internal bool Force { get; set; }

        internal string Execute()
        {
            var output = chainManger.ResolveChainFileName(Output);
            if (fileSystem.File.Exists(output))
            {
                if (Force)
                {
                    fileSystem.File.Delete(output);
                }
                else
                {
                    throw new Exception("You must specify --force to overwrite an existing file");
                }
            }

            if (fileSystem.File.Exists(output))
            {
                throw new ArgumentException($"{output} already exists", nameof(output));
            }

            var chain = chainManger.CreateChain(Count);
            chainManger.SaveChain(chain, output);

            return output;
        }

        internal int OnExecute(IConsole console)
        {
            try
            {
                var output = Execute();

                console.Out.WriteLine($"Created {Count} node privatenet at {output}");
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
