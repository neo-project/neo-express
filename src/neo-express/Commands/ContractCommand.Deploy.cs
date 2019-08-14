using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    internal partial class ContractCommand
    {
        [Command(Name = "deploy")]
        private class Deploy
        {
            [Argument(0)]
            [Required]
            string Contract { get; }

            [Argument(1)]
            [Required]
            string Account { get; }

            [Option]
            private string Input { get; }


            async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var contract = chain.GetContract(Contract);
                    if (contract == default)
                    {
                        throw new Exception($"Contract {Contract} not found.");
                    }

                    var account = chain.GetAccount(Account);
                    if (account == default)
                    {
                        throw new Exception($"Account {Account} not found.");
                    }

                    var uri = chain.GetUri();
                    var result = await NeoRpcClient.ExpressDeployContract(uri, contract, account.ScriptHash).ConfigureAwait(false);
                    console.WriteLine(result.ToString(Formatting.Indented));

                    var txid = result["txid"];
                    if (txid != null)
                    {
                        console.WriteLine("deployment complete");
                    }
                    else
                    {
                        // TODO: DRY + standardize genesis vs. standard signing
                        var data = result.Value<string>("hash-data").ToByteArray();
                        var signatures = new JArray(account.Sign(data));
                        var result2 = await NeoRpcClient.ExpressSubmitSignatures(uri, result["contract-context"], signatures).ConfigureAwait(false);
                        console.WriteLine(result2.ToString(Formatting.Indented));
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
