using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command("isBlocked", "blocked", Description = "Unblock account for usage")]
        internal class IsBlocked
        {
            readonly IFileSystem fileSystem;

            public IsBlocked(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            [Argument(0, Description = "Account to check block status of")]
            [Required]
            internal string ScriptHash { get; init; } = string.Empty;

            
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    await Task.CompletedTask;
                    // var (chain, _) = fileSystem.LoadExpressChain(Input);
                    // using var expressNode = chain.GetExpressNode(fileSystem);

                    // var parsedHash = await expressNode.ParseBlockableScriptHashAsync(ScriptHash).ConfigureAwait(false);
                    // var isBlocked = await expressNode.GetIsBlockedAsync(parsedHash).ConfigureAwait(false);
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