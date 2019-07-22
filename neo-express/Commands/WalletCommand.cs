using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;

namespace Neo.Express.Commands
{
    [Command("wallet")]
    [Subcommand(typeof(Create), typeof(Delete), typeof(Export), typeof(List))]
    internal class WalletCommand
    {
        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }

        [Command("create")]
        private class Create
        {
            [Argument(0)]
            [Required]
            private string Name { get; }

            [Option]
            private bool Force { get; }

            [Option]
            private string Input { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                var input = Program.DefaultPrivatenetFileName(Input);
                if (!File.Exists(input))
                {
                    console.WriteLine($"{input} doesn't exist");
                    app.ShowHelp();
                    return 1;
                }

                var devchain = DevChain.Load(input);
                if (devchain.IsReservedName(Name))
                {
                    console.WriteLine($"{Name} is a reserved name. Choose a different wallet name.");
                    app.ShowHelp();
                    return 1;
                }

                if (!Force && (devchain.GetWallet(Name) != default))
                {
                    console.WriteLine($"{Name} dev wallet already exists. Use --force to overwrite.");
                    app.ShowHelp();
                    return 1;
                }

                var wallet = new DevWallet(Name);
                var account = wallet.CreateAccount();

                console.WriteLine($"{Name}\n\t{account.Address}");
                devchain.Wallets.Add(wallet);

                devchain.Save(input);
                return 0;
            }
        }

        [Command("delete")]
        private class Delete
        {
            [Argument(0)]
            [Required]
            private string Name { get; }

            [Option]
            private bool Force { get; }

            [Option]
            private string Input { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                var input = Program.DefaultPrivatenetFileName(Input);
                if (!File.Exists(input))
                {
                    console.WriteLine($"{input} doesn't exist");
                    app.ShowHelp();
                    return 1;
                }

                var devchain = DevChain.Load(input);
                var wallet = devchain.GetWallet(Name);
                if (wallet == default)
                {
                    console.WriteLine($"{Name} privatenet wallet not found.");
                }
                else
                {
                    if (!Force)
                    {
                        console.WriteLine("You must specify force to delete a privatenet wallet.");
                        app.ShowHelp();
                        return 1;
                    }

                    devchain.Wallets.Remove(wallet);
                    devchain.Save(input);
                    console.WriteLine($"{Name} privatenet wallet deleted.");
                }

                return 0;
            }
        }

        [Command("export")]
        private class Export
        {
            [Argument(0)]
            [Required]
            private string Name { get; }

            [Option]
            private string Input { get; }

            [Option]
            private string Output { get; }

            [Option]
            private bool Force { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                var input = Program.DefaultPrivatenetFileName(Input);
                if (!File.Exists(input))
                {
                    console.WriteLine($"{input} doesn't exist");
                    app.ShowHelp();
                    return 1;
                }

                var output = string.IsNullOrEmpty(Output)
                   ? Path.Combine(Directory.GetCurrentDirectory(), $"{Name}.wallet.json")
                   : Output;

                if (File.Exists(output))
                {
                    if (Force)
                    {
                        File.Delete(output);
                    }
                    else
                    {
                        console.WriteLine("You must specify force to overwrite an exported wallet.");
                        app.ShowHelp();
                        return 1;
                    }
                }

                var devchain = DevChain.Load(input);
                var wallet = devchain.GetWallet(Name);
                if (wallet == (default))
                {
                    console.WriteLine($"{Name} privatenet wallet not found.");
                }
                else
                {
                    var password = Prompt.GetPassword("Input password to use for exported wallet");
                    wallet.Export(output, password);
                    console.WriteLine($"{Name} privatenet wallet exported to {output}");
                }

                return 0;
            }
        }

        [Command("list")]
        private class List
        {
            [Option]
            private string Input { get; }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                var input = Program.DefaultPrivatenetFileName(Input);
                if (!File.Exists(input))
                {
                    console.WriteLine($"{input} doesn't exist");
                    app.ShowHelp();
                    return 1;
                }

                var devchain = DevChain.Load(input);
                foreach (var wallet in devchain.Wallets)
                {
                    console.WriteLine(wallet.Name);

                    foreach (var a in wallet.GetAccounts())
                    {
                        console.WriteLine($"    {a.Address}");
                        console.WriteLine($"    {a.ScriptHash}");
                    }
                }

                return 0;
            }
        }
    }
}
