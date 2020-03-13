using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoExpress.Neo2.Commands
{
    internal partial class ContractCommand
    {
        [Command(Name = "storage")]
        private class Storage
        {
            [Argument(0)]
            [Required]
            string Contract { get; } = string.Empty;

            [Option]
            private string Input { get; } = string.Empty;

            [Option]
            private bool Json { get; } = false;

            [Option]
            private bool Overwrite { get; }

            async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                void WriteStorage(JToken results)
                {
                    foreach (var kvp in results)
                    {
                        var key = kvp.Value<string>("key").ToByteArray();
                        var value = kvp.Value<string>("value").ToByteArray();

                        console.Write("0x");
                        console.WriteLine(key.ToHexString(true));
                        console.Write("  key (as string)   : ");
                        console.WriteLine(Encoding.UTF8.GetString(key));
                        console.Write("  value (as bytes)  : 0x");
                        console.WriteLine(value.ToHexString(true));
                        console.Write("        (as string) : ");
                        console.WriteLine(Encoding.UTF8.GetString(value));
                        console.WriteLine($"  constant value    : {kvp.Value<bool>("constant")}");
                    }
                }

                void WriteStorageAsJson(JToken results)
                {
                    console.WriteLine("\"storage\": [");

                    foreach (var kvp in results)
                    {
                        var key = kvp.Value<string>("key").ToByteArray();
                        var value = kvp.Value<string>("value").ToByteArray();

                        console.WriteLine("  {");
                        if (kvp.Value<bool>("constant"))
                        {
                            console.WriteLine($"    \"constant\": true,");
                        }
                        console.WriteLine($"    \"key\": \"0x{key.ToHexString(true)}\",");
                        console.WriteLine($"    \"value\": \"0x{value.ToHexString(true)}\"");
                        console.WriteLine("  },");
                    }
                    console.WriteLine("],");
                }

                try
                {
                    var (chain, filename) = Program.LoadExpressChain(Input);
                    var contract = chain.GetContract(Contract);
                    if (contract == null)
                    {
                        throw new Exception($"Contract {Contract} not found.");
                    }

                    var uri = chain.GetUri();
                    var result = await NeoRpcClient.ExpressGetContractStorage(uri, contract.Hash);

                    if (result != null && result.Any())
                    {
                        if (Json)
                        {
                            WriteStorageAsJson(result);
                        }
                        else
                        {
                            WriteStorage(result);
                        }
                    }
                    else
                    {
                        console.WriteLine($"no storages for {Contract} contract");
                    }

                    chain.SaveContract(contract, filename, console, Overwrite);
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
