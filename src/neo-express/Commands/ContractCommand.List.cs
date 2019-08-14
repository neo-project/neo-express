using McMaster.Extensions.CommandLineUtils;
using System;

namespace NeoExpress.Commands
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
                    var (chain, _) = Program.LoadExpressChain(Input);
                    foreach (var c in chain.Contracts)
                    {
                        console.WriteLine($"{c.Name}");
                        console.WriteLine($"\t{c.Hash}");
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    console.WriteError(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }
        }
    }
}
