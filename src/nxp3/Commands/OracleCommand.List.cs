using System;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Abstractions.Models;
using Neo.Cryptography.ECC;

namespace nxp3.Commands
{
    partial class OracleCommand
    {
        [Command("list")]
        class List
        {
            [Option]
            string Input { get; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var blockchainOperations = new NeoExpress.Neo3.BlockchainOperations();
                    var result = await blockchainOperations
                        .GetOracleRoles(chain)
                        .ConfigureAwait(false);
                    
                    if (result.State == Neo.VM.VMState.HALT
                        && result.Stack.Length >= 1
                        && result.Stack[0] is Neo.VM.Types.Array array)
                    {
                        console.WriteLine($"Oracle Nodes ({array.Count}): ");
                        for (var x = 0; x < array.Count; x++)
                        {
                            var point = ECPoint.DecodePoint(array[x].GetSpan(), ECCurve.Secp256r1);
                            console.WriteLine($"  {point}");
                        }
                    }

                    return 0;
                }
                catch (Exception ex)
                {
                    console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }
        }
    }
}
