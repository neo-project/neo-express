using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Express.Commands
{
    internal partial class ContractCommand
    {
        [Command(Name = "get")]
        private class Get
        {
            [Argument(0)]
            [Required]
            string Contract { get; }

            [Option]
            private string Input { get; }


            async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var input = Program.DefaultPrivatenetFileName(Input);
                    if (!File.Exists(input))
                    {
                        throw new Exception($"{input} input doesn't exist");
                    }

                    var devchain = DevChain.Load(input);
                    var contract = devchain.Contracts.SingleOrDefault(c => c.Name == Contract);
                    if (contract == default)
                    {
                        throw new Exception($"Contract {Contract} not found.");
                    }

                    var uri = devchain.GetUri();
                    var result = await NeoRpcClient.GetContractState(uri, contract.Hash);
                    console.WriteLine(result.ToString(Formatting.Indented));

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
