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
        readonly ExpressChainManagerFactory chainManagerFactory;
        readonly TransactionExecutorFactory txExecutorFactory;
        readonly IFileSystem fileSystem;

        public BatchCommand(ExpressChainManagerFactory chainManagerFactory, IFileSystem fileSystem, TransactionExecutorFactory txExecutorFactory)
        {
            this.chainManagerFactory = chainManagerFactory;
            this.fileSystem = fileSystem;
            this.txExecutorFactory = txExecutorFactory;
        }

        [Argument(0, Description = "Path to batch file to run")]
        [Required]
        internal string BatchFile { get; init; } = string.Empty;

        [Option("-r|--reset[:<CHECKPOINT>]", CommandOptionType.SingleOrNoValue,
            Description = "Reset blockchain to genesis or specified checkpoint before running batch file commands")]
        internal (bool hasValue, string value) Reset { get; init; }

        [Option(Description = "Enable contract execution tracing")]
        internal bool Trace { get; init; } = false;

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
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
                app.WriteException(ex);
                return 1;
            }
        }

        internal async Task ExecuteAsync(ReadOnlyMemory<string> commands, System.IO.TextWriter writer)
        {
            var (chainManager, _) = chainManagerFactory.LoadChain(Input);
            if (chainManager.IsRunning())
            {
                throw new Exception("Cannot run batch command while blockchain is running");
            }

            if (Reset.hasValue)
            {
                if (string.IsNullOrEmpty(Reset.value))
                {
                    for (int i = 0; i < chainManager.Chain.ConsensusNodes.Count; i++)
                    {
                        var node = chainManager.Chain.ConsensusNodes[i];
                        await writer.WriteLineAsync($"Resetting Node {node.Wallet.Name}");
                        chainManager.ResetNode(node, true);
                    }
                }
                else
                {
                    await writer.WriteLineAsync($"Restoring checkpoint {Reset.value}");
                    chainManager.RestoreCheckpoint(Reset.value, true);
                }
            }

            using var txExec = txExecutorFactory.Create(chainManager, Trace, false);

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
                    case CommandLineApplication<BatchFileCommands.Checkpoint.Create> cmd:
                        {
                            _ = await chainManager.CreateCheckpointAsync(
                                txExec.ExpressNode,
                                cmd.Model.Name,
                                cmd.Model.Force,
                                writer).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Contract.Deploy> cmd:
                        {
                            await txExec.ContractDeployAsync(
                                cmd.Model.Contract,
                                cmd.Model.Account,
                                cmd.Model.Password,
                                cmd.Model.WitnessScope,
                                cmd.Model.Force).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Contract.Invoke> cmd:
                        {
                            var script = await txExec.LoadInvocationScriptAsync(
                                cmd.Model.InvocationFile).ConfigureAwait(false);
                            await txExec.ContractInvokeAsync(
                                script,
                                cmd.Model.Account,
                                cmd.Model.Password,
                                cmd.Model.WitnessScope).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Contract.Run> cmd:
                        {
                            var script = await txExec.BuildInvocationScriptAsync(
                                cmd.Model.Contract,
                                cmd.Model.Method,
                                cmd.Model.Arguments).ConfigureAwait(false);
                            await txExec.ContractInvokeAsync(
                                script,
                                cmd.Model.Account,
                                cmd.Model.Password,
                                cmd.Model.WitnessScope).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.FastForward> cmd:
                        {
                            await txExec.ExpressNode.FastForwardAsync(cmd.Model.Count).ConfigureAwait(false);
                            await writer.WriteLineAsync($"{cmd.Model.Count} empty blocks minted").ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Oracle.Enable> cmd:
                        {
                            await txExec.OracleEnableAsync(
                                cmd.Model.Account,
                                cmd.Model.Password).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Oracle.Response> cmd:
                        {
                            await txExec.OracleResponseAsync(
                                cmd.Model.Url,
                                cmd.Model.ResponsePath).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Policy.Block> cmd:
                        {
                            await txExec.BlockAsync(
                                cmd.Model.ScriptHash,
                                cmd.Model.Account,
                                cmd.Model.Password).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Policy.Set> cmd:
                        {
                            await txExec.SetPolicyAsync(
                                cmd.Model.Policy,
                                cmd.Model.Value,
                                cmd.Model.Account,
                                cmd.Model.Password).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Policy.Unblock> cmd:
                        {
                            await txExec.UnblockAsync(
                                cmd.Model.ScriptHash,
                                cmd.Model.Account,
                                cmd.Model.Password).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Transfer> cmd:
                        {
                            await txExec.TransferAsync(
                                cmd.Model.Quantity,
                                cmd.Model.Asset,
                                cmd.Model.Sender,
                                cmd.Model.Password,
                                cmd.Model.Receiver).ConfigureAwait(false);
                            break;
                        }
                    default:
                        throw new Exception($"Unknown batch command {pr.SelectedCommand.GetType()}");
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
