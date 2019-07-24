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
                    var (devChain, _) = DevChain.Load(Input);
                    foreach (var c in devChain.Contracts)
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
