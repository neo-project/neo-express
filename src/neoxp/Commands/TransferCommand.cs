using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("transfer", Description = "Transfer asset between accounts")]
    class TransferCommand
    {
        readonly IExpressChain chain;

        public TransferCommand(IExpressChain chain)
        {
            this.chain = chain;
        }

        public TransferCommand(CommandLineApplication app)
        {
            this.chain = app.GetExpressFile();
        }

        [Argument(0, Description = "Amount to transfer")]
        [Required]
        internal string Quantity { get; init; } = string.Empty;

        [Argument(1, Description = "Asset to transfer (symbol or script hash)")]
        [Required]
        internal string Asset { get; init; } = string.Empty;

        [Argument(2, Description = "Account to send asset from")]
        [Required]
        internal string Sender { get; init; } = string.Empty;

        [Argument(3, Description = "Account to send asset to")]
        [Required]
        internal string Receiver { get; init; } = string.Empty;

        [Option(Description = "password to use for NEP-2/NEP-6 sender")]
        internal string Password { get; init; } = string.Empty;

        [Option(Description = "Enable contract execution tracing")]
        internal bool Trace { get; init; } = false;

        [Option(Description = "Output as JSON")]
        internal bool Json { get; init; } = false;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                await Task.CompletedTask;
                // var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                // var password = chainManager.Chain.ResolvePassword(Sender, Password);
                // using var txExec = txExecutorFactory.Create(chainManager, Trace, Json);
                // await txExec.TransferAsync(Quantity, Asset, Sender, password, Receiver).ConfigureAwait(false);
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
