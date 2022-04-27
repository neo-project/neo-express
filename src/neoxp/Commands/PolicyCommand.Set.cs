using System;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Models;

namespace NeoExpress.Commands
{
    partial class PolicyCommand
    {
        [Command(Name = "set", Description = "Set single policy value")]
        internal class Set
        {
            readonly IFileSystem fileSystem;

            public Set(IFileSystem fileSystem)
            {
                this.fileSystem = fileSystem;
            }

            [Argument(0, Description = "Policy to set")]
            [Required]
            internal PolicySettings Policy { get; init; }

            [Argument(1, Description = "New Policy Value")]
            [Required]
            internal decimal Value { get; set; }

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
                    using var txExec = new TransactionExecutor(fileSystem, chainManager, Trace, Json, console.Out); 

                    await txExec.SetPolicyAsync(Policy, Value, Account, Password).ConfigureAwait(false);
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
