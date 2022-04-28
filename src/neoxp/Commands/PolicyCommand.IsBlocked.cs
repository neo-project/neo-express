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

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chain, _) = fileSystem.LoadExpressChain(Input);
                    using var expressNode = chain.GetExpressNode(fileSystem);

                    var scriptHash = await expressNode.ParseScriptHashAsync(ScriptHash).ConfigureAwait(false);
                    if (scriptHash.IsT1)
                    {
                        throw new Exception($"{ScriptHash} script hash not found or not supported");
                    }

                    var isBlocked = await expressNode.GetIsBlockedAsync(scriptHash.AsT0).ConfigureAwait(false);
                    await console.Out.WriteLineAsync($"{ScriptHash} account is {(isBlocked ? "" : "not ")}blocked");
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