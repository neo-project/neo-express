using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command(Name = "unblock", Description = "Unblock account for usage")]
        internal class Unblock
        {
            readonly IFileSystem fileSystem;

            public Unblock(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            [Argument(0, Description = "Account to unblock")]
            [Required]
            internal string ScriptHash { get; init; } = string.Empty;

            [Argument(2, Description = "Account to pay contract invocation GAS fee")]
            [Required]
            internal string Account { get; init; } = string.Empty;

            [Option(Description = "password to use for NEP-2/NEP-6 sender")]
            internal string Password { get; init; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Enable contract execution tracing")]
            internal bool Trace { get; init; } = false;

            [Option(Description = "Output as JSON")]
            internal bool Json { get; init; } = false;

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var (chainManager, _) = fileSystem.LoadChainManager(Input);
                    var password = chainManager.Chain.ResolvePassword(Account, Password);
                    using var txExec = new TransactionExecutor(fileSystem, chainManager, Trace, Json, console.Out); 
                    await txExec.UnblockAsync(ScriptHash, Account, Password).ConfigureAwait(false);
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
