using McMaster.Extensions.CommandLineUtils;
using Neo;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;

namespace NeoExpress.Commands
{
    [Command("transfernft", Description = "Transfer NFT asset between accounts")]
    class TransferNFTCommand
    {
        readonly ExpressChainManagerFactory chainManagerFactory;
        readonly TransactionExecutorFactory txExecutorFactory;

        public TransferNFTCommand(ExpressChainManagerFactory chainManagerFactory, TransactionExecutorFactory txExecutorFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
            this.txExecutorFactory = txExecutorFactory;
        }

        [Argument(0, Description = "NFT Contract (symbol or script hash)")]
        [Required]
        internal string Contract { get; init; } = string.Empty;

        [Argument(1, Description = "TokenId of NFT (Hex string)")]
        [Required]
        internal string TokenId { get; init; } = string.Empty;

        [Argument(2, Description = "Account to send NFT from")]
        [Required]
        internal string Sender { get; init; } = string.Empty;

        [Argument(3, Description = "Account to send NFT to")]
        [Required]
        internal string Receiver { get; init; } = string.Empty;

        [Option(Description = "Optional data parameter to pass to transfer operation")]
        internal string Data { get; init; } = string.Empty;

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
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                var password = chainManager.Chain.ResolvePassword(Sender, Password);
                using var txExec = txExecutorFactory.Create(chainManager, Trace, Json);
                await txExec.TransferNFTAsync(Contract, HexStringToUTF8(TokenId), Sender, password, Receiver, Data).ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex)
            {
                app.WriteException(ex);
                return 1;
            }

            static string HexStringToUTF8(string hex)
            {
                hex = hex.ToLower().Trim();
                if (!new Regex("^(0x)?([0-9a-f]{2})+$").IsMatch(hex))
                    throw new FormatException();

                if (new Regex("^([0-9a-f]{2})+$").IsMatch(hex))
                {
                    return Encoding.UTF8.GetString(hex.HexToBytes());
                }
                else
                {
                    return Encoding.UTF8.GetString(hex[2..].HexToBytes().Reverse().ToArray());
                }
            }
        }
    }
}
