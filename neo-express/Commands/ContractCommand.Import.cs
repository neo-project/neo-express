using McMaster.Extensions.CommandLineUtils;
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
                System.Diagnostics.Debug.Assert(File.Exists(avmFile));

                var abiJsonFile = Path.ChangeExtension(avmFile, ".abi.json");
                if (!File.Exists(abiJsonFile))
                {
                    throw new Exception($"there is no .abi.json file for {avmFile}.");
                }

                var mdJsonFile = Path.ChangeExtension(avmFile, ".md.json");
                if (!File.Exists(mdJsonFile))
                {
                    throw new Exception($"there is no .md.json file for {avmFile}.");
                }

                var contract = DevContract.Load(avmFile, abiJsonFile, mdJsonFile);
                var (devChain, filename) = DevChain.Load(Input);


                if (!Force && devChain.Contracts.Any(c => c.Name == contract.Name))
                {
                    throw new Exception($"{contract.Name} dev contract already exists. Use --force to overwrite.");
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
                    console.WriteLine(ex.Message);
                    app.ShowHelp();
                    return 1;
                }
            }
        }
    }
}
