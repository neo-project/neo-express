using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Express.Commands
{
    internal partial class ContractCommand
    {
        [Command(Name = "storage")]
        private class Storage
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
                    var (devChain, _) = DevChain.Load(Input);
                    var contract = devChain.Contracts.SingleOrDefault(c => c.Name == Contract);
                    if (contract == default)
                    {
                        throw new Exception($"Contract {Contract} not found.");
                    }

                    var uri = devChain.GetUri();
                    var result = await NeoRpcClient.ExpressGetContractStorage(uri, contract.Hash);

                    if (result != null && result.Any())
                    {
                        foreach (var kvp in result)
                        {
                            var key = kvp.Value<string>("key").HexToBytes();
                            var value = kvp.Value<string>("value").HexToBytes();
                            var constant = kvp.Value<bool>("constant");

                            console.Write("0x");
                            console.WriteLine(key.ToHexString());
                            console.Write("  key (as string)   : ");
                            console.WriteLine(Encoding.UTF8.GetString(key));
                            console.Write("  value (as bytes)  : 0x");
                            console.WriteLine(value.ToHexString());
                            console.Write("        (as string) : ");
                            console.WriteLine(Encoding.UTF8.GetString(value));
                            console.WriteLine($"  constant value    : {constant}");
                        }
                    }
                    else
                    {
                        console.WriteLine($"no storages for {Contract} contract");
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
