using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Neo3;
using NeoExpress.Abstractions;
using System.Text;
using System;

namespace nxp3.Commands
{
    partial class ContractCommand
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

                    var blockchainOperations = new BlockchainOperations();
                    var storages = await blockchainOperations.GetStorages(chain, Contract);

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
                        if (value.Length == 20)
                        {
                            var hash = new Neo.UInt160(value);
                            var address = Neo.Wallets.Helper.ToAddress(hash);
                            console.WriteLine($"        (as addr)   : {address}");
                        }
                        console.WriteLine($"  constant value    : {storage.Constant}");
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
