using McMaster.Extensions.CommandLineUtils;
using Neo.Ledger;
using System;
using System.IO;
using System.Linq;

namespace Neo.Express.Commands
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
                DevContract LoadDevContract()
                {
                    var abiJsonFile = Path.ChangeExtension(avmFile, ".abi.json");
                    if (!File.Exists(abiJsonFile))
                    {
                        throw new Exception($"there is no .abi.json file for {avmFile}.");
                    }

                    var mdJsonFile = Path.ChangeExtension(avmFile, ".md.json");
                    if (File.Exists(mdJsonFile))
                    {
                        return DevContract.Load(avmFile, abiJsonFile, mdJsonFile);
                    }

                    var contractPropertyState = ContractPropertyState.NoProperty;
                    if (Prompt.GetYesNo("Does this contract use storage?", false)) contractPropertyState |= ContractPropertyState.HasStorage;
                    if (Prompt.GetYesNo("Does this contract use dynamic invoke?", false)) contractPropertyState |= ContractPropertyState.HasDynamicInvoke;
                    if (Prompt.GetYesNo("Is this contract payable?", false)) contractPropertyState |= ContractPropertyState.Payable;

                    return DevContract.Load(avmFile, abiJsonFile, contractPropertyState);
                }

                System.Diagnostics.Debug.Assert(File.Exists(avmFile));

                var contract = LoadDevContract();
                var (devChain, filename) = DevChain.Load(Input);

                var existingContract = devChain.Contracts.SingleOrDefault(c => c.Name == contract.Name);
                if (existingContract != null)
                {
                    if (!Force)
                    {
                        throw new Exception($"{contract.Name} dev contract already exists. Use --force to overwrite.");
                    }

                    devChain.Contracts.Remove(existingContract);
                }

                devChain.Contracts.Add(contract);
                devChain.Save(filename);

                return 0;
            }

            private int OnExecute(CommandLineApplication app, IConsole console)
            {
                try
                {
                    if ((File.GetAttributes(ContractPath) & FileAttributes.Directory) != 0)
                    {
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
