using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo2;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoExpress.Commands
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

            async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var hash = GetScriptHash(Contract);

                    var blockchainOperations = new BlockchainOperations();
                    var storages = await blockchainOperations.GetStorage(chain, hash);

                    foreach (var storage in storages)
                    {
                        var key = storage.Key.ToByteArray();
                        var value = storage.Value.ToByteArray();

                        console.Write("0x");
                        console.WriteLine(key.ToHexString(true));
                        console.Write("  key (as string)   : ");
                        console.WriteLine(Encoding.UTF8.GetString(key));
                        console.Write("  value (as bytes)  : 0x");
                        console.WriteLine(value.ToHexString(true));
                        console.Write("        (as string) : ");
                        console.WriteLine(Encoding.UTF8.GetString(value));
                        console.WriteLine($"  constant value    : {storage.Constant}");
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
