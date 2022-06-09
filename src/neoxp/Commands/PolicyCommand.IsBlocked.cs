using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command("isBlocked", "blocked", Description = "Unblock account for usage")]
        internal class IsBlocked
        {
            readonly IExpressChain chain;

            public IsBlocked(IExpressChain chain)
            {
                this.chain = chain;
            }

            public IsBlocked(CommandLineApplication app)
            {
                this.chain = app.GetExpressFile();
            }

            [Argument(0, Description = "Account to check block status of")]
            [Required]
            internal string ScriptHash { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    // var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    // using var expressNode = chainManager.GetExpressNode();

                    // var scriptHash = await expressNode.ParseScriptHashToBlockAsync(chainManager.Chain, ScriptHash).ConfigureAwait(false);
                    // if (scriptHash.IsT1)
                    // {
                    //     throw new Exception($"{ScriptHash} script hash not found or not supported");
                    // }

                    // var isBlocked = await expressNode.GetIsBlockedAsync(scriptHash.AsT0).ConfigureAwait(false);
                    // await console.Out.WriteLineAsync($"{ScriptHash} account is {(isBlocked ? "" : "not ")}blocked");
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }
        }
    }
}