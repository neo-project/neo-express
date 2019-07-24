using McMaster.Extensions.CommandLineUtils;
using System;
using System.IO;

namespace Neo.Express.Commands
{
    internal partial class ContractCommand
    {
        [Command(Name = "list")]
        private class List
        {
            [Option]
            private string Input { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var input = Program.DefaultPrivatenetFileName(Input);
                    if (!File.Exists(input))
                    {
                        throw new Exception($"{input} input doesn't exist");
                    }

                    var devchain = DevChain.Load(input);

                    foreach (var c in devchain.Contracts)
                    {
                        console.WriteLine($"{c.Name} - {c.Title}");
                        console.WriteLine($"\t{c.Hash}");
                    }

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
}
