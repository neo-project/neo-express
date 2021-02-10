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
        [Argument(0, Description = "Path to batch file to run")]
        internal string BatchFile { get; } = string.Empty;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; } = string.Empty;

        [Option(Description = "Reset blockchain before running batch file commands")]
        internal bool Reset { get; set; } = false;

        readonly IExpressChainManagerFactory chainManagerFactory;
        readonly IFileSystem fileSystem;

        public BatchCommand(IExpressChainManagerFactory chainManagerFactory, IFileSystem fileSystem)
        {
            this.chainManagerFactory = chainManagerFactory;
            this.fileSystem = fileSystem;
        }

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
                    chainManager.ResetNode(chainManager.Chain.ConsensusNodes[i], true);
                }
            }

            using var expressNode = chainManager.GetExpressNode();

            var batchApp = new CommandLineApplication<RootBatchCommand>();
            batchApp.Conventions.UseDefaultConventions();

            for (var i = 0; i < commands.Length; i++)
            {
                var args = SplitCommandLine(commands.Span[i]).ToArray();
                var pr = batchApp.Parse(args);
                switch (pr.SelectedCommand)
                {
                    case CommandLineApplication<BatchTransfer> transferApp: 
                    {
                        var model = transferApp.Model;
                        await TransferCommand.ExecuteAsync(
                            chainManager, 
                            expressNode, 
                            model.Quantity, 
                            model.Asset, 
                            model.Sender, 
                            model.Receiver, 
                            writer).ConfigureAwait(false);
                        break;
                    }
                    case CommandLineApplication<BatchCheckpointCreate> checkpointCreateApp: 
                    {
                        var model = checkpointCreateApp.Model;
                        await CheckpointCommand.Create.ExecuteAsync(
                            chainManager, 
                            model.Name,
                            model.Force,
                            writer).ConfigureAwait(false);
                        break;
                    }
                    case CommandLineApplication<BatchContractDeploy> deployApp: 
                    {
                        var model = deployApp.Model;
                        await ContractCommand.Deploy.ExecuteAsync(
                            chainManager,
                            expressNode,
                            fileSystem,
                            model.Contract,
                            model.Account,
                            writer).ConfigureAwait(false);
                        break;
                    }
                    case CommandLineApplication<BatchContractInvoke> invokeApp: 
                    {
                        var model = invokeApp.Model;
                        await ContractCommand.Invoke.ExecuteTxAsync(
                            chainManager,
                            expressNode,
                            model.InvocationFile,
                            model.Account,
                            fileSystem,
                            writer).ConfigureAwait(false);
                        break;
                    }
                    default:
                        throw new Exception();
                }
            }
        }

        [Command]
        [Subcommand(typeof(BatchCheckpoint), typeof(BatchContract), typeof(BatchTransfer))]
        internal class RootBatchCommand
        {
        }

        [Command("transfer")]
        internal class BatchTransfer
        {
            [Argument(0, Description = "Amount to transfer")]
            [Required]
            internal string Quantity { get; } = string.Empty;

            [Argument(1, Description = "Asset to transfer (symbol or script hash)")]
            [Required]
            internal string Asset { get; } = string.Empty;

            [Argument(2, Description = "Account to send asset from")]
            [Required]
            internal string Sender { get; } = string.Empty;

            [Argument(3, Description = "Account to send asset to")]
            [Required]
            internal string Receiver { get; } = string.Empty;
        }

        [Command("checkpoint")]
        [Subcommand(typeof(BatchCheckpointCreate))]
        internal class BatchCheckpoint
        {
        }

        [Command("create")]
        internal class BatchCheckpointCreate
        {
            [Argument(0, "Checkpoint file name")]
            [Required]
            internal string Name { get; } = string.Empty;

            [Option(Description = "Overwrite existing data")]
            internal bool Force { get; }
        }

        [Command("contract")]
        [Subcommand(typeof(BatchContractDeploy), typeof(BatchContractInvoke))]
        internal class BatchContract
        {
        }

        [Command("deploy")]
        internal class BatchContractDeploy
        {
            [Argument(0, Description = "Path to contract .nef file")]
            [Required]
            internal string Contract { get; } = string.Empty;

            [Argument(1, Description = "Account to pay contract deployment GAS fee")]
            [Required]
            internal string Account { get; } = string.Empty;

        }


        [Command("invoke")]
        internal class BatchContractInvoke
        {
            [Argument(0, Description = "Path to contract invocation JSON file")]
            [Required]
            internal string InvocationFile { get; } = string.Empty;

            [Argument(1, Description = "Account to pay contract invocation GAS fee")]
            internal string Account { get; } = string.Empty;
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
