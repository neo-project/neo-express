using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NeoExpress.Commands
{
    internal partial class ContractCommand
    {
        [Command(Name = "import")]
        private class Import
        {
            [Argument(0)]
            string ContractPath { get; }

            [Option]
            bool Force { get; }

            [Option]
            private string Input { get; }

            private int ImportContract(string avmFile, IConsole console)
            {
                System.Diagnostics.Debug.Assert(File.Exists(avmFile));
                var (chain, filename) = Program.LoadExpressChain(Input);
                var contract = Program.GetBackend().ImportContract(avmFile);

                if (chain.Contracts == null)
                {
                    chain.Contracts = new List<ExpressContract>(1);
                }
                else
                {
                    var existingContract = chain.Contracts.SingleOrDefault(c => c.Name.Equals(contract.Name, StringComparison.InvariantCultureIgnoreCase));
                    if (existingContract != null)
                    {
                        if (!Force)
                        {
                            throw new Exception($"{contract.Name} dev contract already exists. Use --force to overwrite.");
                        }

                        chain.Contracts.Remove(existingContract);
                    }
                }

                // temporarily ask about storage and dynamic invoke
                // this will change with NEO2 compiler improvements and/or NEO3 manifect
                if (Prompt.GetYesNo("Does this contract use storage?", false))
                {
                    contract.Properties.Add("has-storage", "true");
                }

                if (Prompt.GetYesNo("Does this contract use dynamic invoke?", false))
                {
                    contract.Properties.Add("has-dynamic-invoke", "true");
                }

                chain.Contracts.Add(contract);
                chain.Save(filename);

                return 0;
            }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    if ((File.GetAttributes(ContractPath) & FileAttributes.Directory) != 0)
                    {
                        // extension changes to .nvm in NEO 3
                        var avmFiles = Directory.EnumerateFiles(ContractPath, "*.avm");
                        var avmFileCount = avmFiles.Count();

                        if (avmFileCount == 0)
                        {
                            throw new Exception($"There are no .avm files in {ContractPath}");
                        }

                        if (avmFileCount > 1)
                        {
                            throw new Exception($"There are more than one .avm files in {ContractPath}. Please specify file name directly");
                        }

                        return ImportContract(avmFiles.Single(), console);
                    }

                    if (!File.Exists(ContractPath))
                    {
                        throw new Exception($"There is no .avm file at {ContractPath}");
                    }

                    if (Path.GetExtension(ContractPath) != ".avm")
                    {
                        throw new Exception($"{ContractPath} is not an .avm file.");
                    }

                    return ImportContract(ContractPath, console);
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
