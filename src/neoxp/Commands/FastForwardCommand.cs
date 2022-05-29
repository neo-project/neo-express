using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("fastfwd", Description = "Mint empty blocks to fast forward the block chain")]
    class FastForwardCommand
    {
        readonly IExpressChain expressFile;

        public FastForwardCommand(IExpressChain expressFile)
        {
            this.expressFile = expressFile;
        }

        public FastForwardCommand(CommandLineApplication app) : this(app.GetExpressFile())
        {
        }

        [Argument(0, Description = "Number of blocks to mint")]
        [Required]
        internal uint Count { get; init; }

        [Option(Description = "Timestamp delta for last generated block")]
        internal string TimestampDelta { get; init; } = string.Empty;

        internal Task<int> OnExecuteAsync(CommandLineApplication app)
            => app.ExecuteAsync(this.ExecuteAsync);

        internal async Task ExecuteAsync(IConsole console)
        {
            using var expressNode = expressFile.GetExpressNode();
            await ExecuteAsync(expressNode, Count, TimestampDelta).ConfigureAwait(false);
            await console.Out.WriteLineAsync($"{Count} empty blocks minted").ConfigureAwait(false);
        }

        public static async Task ExecuteAsync(IExpressNode expressNode, uint count, string timestampDelta)
        {
            var delta = string.IsNullOrEmpty(timestampDelta)
                ? TimeSpan.Zero
                : ulong.TryParse(timestampDelta, out var @ulong)
                    ? TimeSpan.FromSeconds(@ulong)
                    : TimeSpan.TryParse(timestampDelta, out var timeSpan)
                        ? timeSpan
                        : throw new Exception($"Could not parse timestamp delta {timestampDelta}");

            await expressNode.FastForwardAsync(count, delta).ConfigureAwait(false);
        }
    }
}
