using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json.Linq;
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

                    var rawTxResponseTask = NeoRpcClient.GetRawTransaction(uri, TransactionId);
                    var appLogResponseTask = NeoRpcClient.GetApplicationLog(uri, TransactionId);
                    await Task.WhenAll(rawTxResponseTask, appLogResponseTask);

                    console.WriteResult(rawTxResponseTask.Result);
                    var appLogResponse = appLogResponseTask.Result ?? JValue.CreateString(string.Empty);
                    if (appLogResponse.Type != JTokenType.String
                        || appLogResponse.Value<string>().Length != 0)
                    {
                        console.WriteResult(appLogResponse);
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
