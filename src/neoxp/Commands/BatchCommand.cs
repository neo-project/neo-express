using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("batch", Description = "Execute a series of offline Neo-Express operations")]
    partial class BatchCommand
    {
        readonly IExpressChainManagerFactory chainManagerFactory;
        readonly IFileSystem fileSystem;

        public BatchCommand(IExpressChainManagerFactory chainManagerFactory, IFileSystem fileSystem)
        {
            this.chainManagerFactory = chainManagerFactory;
            this.fileSystem = fileSystem;
        }

        [Argument(0, Description = "Path to batch file to run")]
        [Required]
        internal string BatchFile { get; init; } = string.Empty;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        [Option(Description = "Reset blockchain before running batch file commands")]
        internal bool Reset { get; init; } = false;

        internal async Task<int> OnExecuteAsync(IConsole console, CancellationToken token)
        {
            try
            {
                if (!fileSystem.File.Exists(BatchFile)) throw new Exception($"Batch file {BatchFile} couldn't be found");
                var commands = await fileSystem.File.ReadAllLinesAsync(BatchFile, token).ConfigureAwait(false);
                await ExecuteAsync(commands, console.Out).ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex)
            {
                await console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
                return 1;
            }
        }

        internal async Task ExecuteAsync(ReadOnlyMemory<string> commands, System.IO.TextWriter writer)
        {
            var (chainManager, _) = chainManagerFactory.LoadChain(Input);

            if (Reset)
            {
                for (int i = 0; i < chainManager.Chain.ConsensusNodes.Count; i++)
                {
                    var node = chainManager.Chain.ConsensusNodes[i];
                    await writer.WriteLineAsync($"Resetting Node {node.Wallet.Name}");
                    chainManager.ResetNode(node, true);
                }
            }

            using var expressNode = chainManager.GetExpressNode();

            var batchApp = new CommandLineApplication<BatchFileCommands>();
            batchApp.Conventions.UseDefaultConventions();

            for (var i = 0; i < commands.Length; i++)
            {
                var args = SplitCommandLine(commands.Span[i]).ToArray();
                if (args.Length == 0
                    || args[0].StartsWith('#')
                    || args[0].StartsWith("//")) continue;

                var pr = batchApp.Parse(args);
                switch (pr.SelectedCommand)
                {
                    case CommandLineApplication<BatchFileCommands.Transfer> cmd:
                        {
                            await TransferCommand.ExecuteAsync(
                                chainManager,
                                expressNode,
                                cmd.Model.Quantity,
                                cmd.Model.Asset,
                                cmd.Model.Sender,
                                cmd.Model.Receiver,
                                writer).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Checkpoint.Create> cmd:
                        {
                            await CheckpointCommand.Create.ExecuteAsync(
                                chainManager,
                                expressNode,
                                cmd.Model.Name,
                                cmd.Model.Force,
                                writer).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Contract.Deploy> cmd:
                        {
                            await ContractCommand.Deploy.ExecuteAsync(
                                chainManager,
                                expressNode,
                                fileSystem,
                                cmd.Model.Contract,
                                cmd.Model.Account,
                                cmd.Model.WitnessScope,
                                writer).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Contract.Invoke> cmd:
                        {
                            await ContractCommand.Invoke.ExecuteTxAsync(
                                chainManager,
                                expressNode,
                                cmd.Model.InvocationFile,
                                cmd.Model.Account,
                                cmd.Model.WitnessScope,
                                fileSystem,
                                writer).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Oracle.Enable> cmd:
                        {
                            await OracleCommand.Enable.ExecuteAsync(
                                chainManager,
                                expressNode,
                                cmd.Model.Account,
                                writer).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Oracle.Response> cmd:
                        {
                            await OracleCommand.Response.ExecuteAsync(
                                chainManager,
                                expressNode,
                                fileSystem,
                                cmd.Model.Url,
                                cmd.Model.ResponsePath,
                                null,
                                writer).ConfigureAwait(false);
                            break;
                        }
                    default:
                        throw new Exception();
                }
            }
        }

        // SplitCommandLine method adapted from CommandLineStringSplitter class in https://github.com/dotnet/command-line-api
        static IEnumerable<string> SplitCommandLine(string commandLine)
        {
            var memory = commandLine.AsMemory();

            var startTokenIndex = 0;

            var pos = 0;

            var seeking = Boundary.TokenStart;
            int? skipQuoteAtIndex = null;

            while (pos < memory.Length)
            {
                var c = memory.Span[pos];

                if (char.IsWhiteSpace(c))
                {
                    switch (seeking)
                    {
                        case Boundary.WordEnd:
                            yield return CurrentToken();
                            startTokenIndex = pos;
                            seeking = Boundary.TokenStart;
                            break;

                        case Boundary.TokenStart:
                            startTokenIndex = pos;
                            break;

                        case Boundary.QuoteEnd:
                            break;
                    }
                }
                else if (c == '\"')
                {
                    switch (seeking)
                    {
                        case Boundary.QuoteEnd:
                            yield return CurrentToken();
                            startTokenIndex = pos;
                            seeking = Boundary.TokenStart;
                            break;

                        case Boundary.TokenStart:
                            startTokenIndex = pos + 1;
                            seeking = Boundary.QuoteEnd;
                            break;

                        case Boundary.WordEnd:
                            seeking = Boundary.QuoteEnd;
                            skipQuoteAtIndex = pos;
                            break;
                    }
                }
                else if (seeking == Boundary.TokenStart)
                {
                    seeking = Boundary.WordEnd;
                    startTokenIndex = pos;
                }

                Advance();

                if (IsAtEndOfInput())
                {
                    switch (seeking)
                    {
                        case Boundary.TokenStart:
                            break;
                        default:
                            yield return CurrentToken();
                            break;
                    }
                }
            }

            void Advance() => pos++;

            string CurrentToken()
            {
                if (skipQuoteAtIndex is null)
                {
                    return memory.Slice(startTokenIndex, IndexOfEndOfToken()).ToString();
                }
                else
                {
                    var beforeQuote = memory.Slice(
                        startTokenIndex,
                        skipQuoteAtIndex.Value - startTokenIndex);

                    var indexOfCharAfterQuote = skipQuoteAtIndex.Value + 1;

                    var afterQuote = memory.Slice(
                        indexOfCharAfterQuote,
                        pos - skipQuoteAtIndex.Value - 1);

                    skipQuoteAtIndex = null;

                    return $"{beforeQuote}{afterQuote}";
                }
            }

            int IndexOfEndOfToken() => pos - startTokenIndex;

            bool IsAtEndOfInput() => pos == memory.Length;
        }

        private enum Boundary
        {
            TokenStart,
            WordEnd,
            QuoteEnd
        }
    }
}
