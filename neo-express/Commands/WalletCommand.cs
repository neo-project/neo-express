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
    class WalletCommand
    {
        int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.WriteLine("You must specify at a subcommand.");
            app.ShowHelp();
            return 1;
        }

        [Command("create")]
        class Create
        {
            [Argument(0)]
            [Required]
            string Name { get; }

            [Option]
            bool Force { get; }

            [Option]
            string Input { get; }

            int OnExecute(CommandLineApplication app, IConsole console)
            {
                var input = Program.DefaultPrivatenetFileName(Input);
                if (!File.Exists(input))
                {
                    console.WriteLine($"{input} doesn't exist");
                    app.ShowHelp();
                    return 1;
                }

                var devchain = DevChain.Load(input);
                if (devchain.Wallets.Any(w => w.Name == Name) && !Force)
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
        class Delete
        {
            [Argument(0)]
            [Required]
            string Name { get; }

            [Option]
            bool Force { get; }

            [Option]
            string Input { get; }

            int OnExecute(CommandLineApplication app, IConsole console)
            {
                if (!Force)
                {
                    console.WriteLine("You must specify force to delete a privatenet wallet.");
                    app.ShowHelp();
                    return 1;
                }

                var input = Program.DefaultPrivatenetFileName(Input);
                if (!File.Exists(input))
                {
                    console.WriteLine($"{input} doesn't exist");
                    app.ShowHelp();
                    return 1;
                }

                var devchain = DevChain.Load(input);
                var wallet = devchain.Wallets.SingleOrDefault(w => w.Name == Name);
                if (wallet != default)
                {
                    devchain.Wallets.Remove(wallet);
                    devchain.Save(input);
                    console.WriteLine($"{Name} privatenet wallet deleted.");
                }
                else
                {
                    console.WriteLine($"{Name} privatenet wallet not found.");
                }

                return 0;
            }
        }

        [Command("export")]
        class Export
        {
            [Argument(0)]
            [Required]
            string Name { get; }

            [Option]
            string Input { get; }

            [Option]
            string Output { get; }

            [Option]
            bool Force { get; }


            int OnExecute(CommandLineApplication app, IConsole console)
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
                var wallet = devchain.Wallets.SingleOrDefault(w => w.Name == Name);
                if (wallet != default)
                {
                    var password = Prompt.GetPassword("Input password to use for exported wallet");
                    wallet.Export(output, password);
                    console.WriteLine($"{Name} privatenet wallet exported to {output}");
                }
                else
                {
                    console.WriteLine($"{Name} privatenet wallet not found.");
                }

                return 0;
            }
        }

        [Command("list")]
        class List
        {
            [Option]
            string Input { get; }

            int OnExecute(CommandLineApplication app, IConsole console)
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
