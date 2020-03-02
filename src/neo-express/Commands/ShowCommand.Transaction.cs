using McMaster.Extensions.CommandLineUtils;
using System;
using System.Threading.Tasks;

namespace NeoExpress.Commands
{
    partial class ShowCommand
    {
        [Command("transaction", "tx")]
        private class Transaction
        {
            [Option]
            private string Input { get; } = string.Empty;

            [Argument(0)]
            private string TransactionId { get; } = string.Empty;

            private async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = Program.LoadExpressChain(Input);
                    var uri = chain.GetUri();

                    var response = await NeoRpcClient.GetRawTransaction(uri, TransactionId);
                    if (response == null)
                    {
                        console.WriteWarning("Requested transaction does not exist or has not been processed.");
                    }
                    else
                    {
                        console.WriteResult(response);
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
