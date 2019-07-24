using McMaster.Extensions.CommandLineUtils;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;

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
                try
                {
                    var input = Program.DefaultPrivatenetFileName(Input);
                    if (!File.Exists(input))
                    {
                        throw new Exception($"{input} doesn't exist");
                    }

                    var devchain = DevChain.Load(input);
                    if (devchain.IsReservedName(Name))
                    {
                        throw new Exception($"{Name} is a reserved name. Choose a different wallet name.");
                    }

                    if (!Force && (devchain.GetWallet(Name) != default))
                    {
                        throw new Exception($"{Name} dev wallet already exists. Use --force to overwrite.");
                    }

                    var wallet = new DevWallet(Name);
                    var account = wallet.CreateAccount();
                    account.IsDefault = true;

                    console.WriteLine($"{Name}\n\t{account.Address}");
                    devchain.Wallets.Add(wallet);

                    devchain.Save(input);
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
                try
                {
                    var input = Program.DefaultPrivatenetFileName(Input);
                    if (!File.Exists(input))
                    {
                        throw new Exception($"{input} doesn't exist");
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
                            throw new Exception("You must specify force to delete a privatenet wallet.");
                        }

                        devchain.Wallets.Remove(wallet);
                        devchain.Save(input);
                        console.WriteLine($"{Name} privatenet wallet deleted.");
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
                try
                {
                    var input = Program.DefaultPrivatenetFileName(Input);
                    if (!File.Exists(input))
                    {
                        throw new Exception($"{input} doesn't exist");
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
                            throw new Exception("You must specify force to overwrite an exported wallet.");
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
                catch (Exception ex)
                {
                    console.WriteLine(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }
        }

        [Command("list")]
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
                        throw new Exception($"{input} doesn't exist");
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
