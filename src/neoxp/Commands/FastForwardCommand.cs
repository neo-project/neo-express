using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("fastfwd", Description = "Mint empty blocks to fast forward the block chain")]
    class FastForwardCommand
    {
        readonly ExpressChainManagerFactory chainManagerFactory;

        public FastForwardCommand(ExpressChainManagerFactory chainManagerFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
        }

        [Argument(0, Description = "Number of blocks to mint")]
        [Required]
        internal uint Count { get; init; }

        [Option(Description = "Timestamp delta for last generated block")]
        internal string TimestampDelta { get; init; } = string.Empty;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            try
            {
                var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                using var expressNode = chainManager.GetExpressNode();

                TimeSpan delta = ParseTimestampDelta(TimestampDelta);
                await expressNode.FastForwardAsync(Count, delta).ConfigureAwait(false);

                await console.Out.WriteLineAsync($"{Count} empty blocks minted").ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex)
            {
                app.WriteException(ex);
                return 1;
            }
        }

        internal static TimeSpan ParseTimestampDelta(string timestampDelta)
            => string.IsNullOrEmpty(timestampDelta)
                ? TimeSpan.Zero
                : ulong.TryParse(timestampDelta, out var @ulong)
                    ? TimeSpan.FromSeconds(@ulong)
                    : TimeSpan.TryParse(timestampDelta, out var timeSpan)
                        ? timeSpan
                        : throw new Exception($"Could not parse timestamp delta {timestampDelta}");
    }
}
