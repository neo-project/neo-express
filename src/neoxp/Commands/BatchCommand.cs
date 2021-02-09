using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace NeoExpress.Commands
{
    [Command("batch", Description = "Execute a series of offline Neo-Express operations")]
    partial class BatchCommand
    {
        [Argument(0, Description = "path to batch file to run")]
        string BatchFile { get; } = string.Empty;

        [Option(Description = "Path to neo-express data file")]
        string Input { get; } = string.Empty;

        readonly IExpressChainManagerFactory chainManagerFactory;
        readonly IFileSystem fileSystem;

        public BatchCommand(IExpressChainManagerFactory chainManagerFactory, IFileSystem fileSystem)
        {
            this.chainManagerFactory = chainManagerFactory;
            this.fileSystem = fileSystem;
        }

        internal async Task ExecuteAsync(IReadOnlyList<string> commands, System.IO.TextWriter writer)
        {
            var (chainManager, _) = chainManagerFactory.LoadChain(Input);
            using var expressNode = chainManager.GetExpressNode();

            var batchCmd = new CommandLineApplication();
            batchCmd.Command("transfer", transferCmd =>
            {
                var quantityArg = transferCmd.Argument("quantity", "Amount to Transfer").IsRequired();
                var assetArg = transferCmd.Argument("asset", "Asset to transfer (symbol or script hash)").IsRequired();
                var senderArg = transferCmd.Argument("sender", "Account to send asset from").IsRequired();
                var receiverArg = transferCmd.Argument("receiver", "Account to send asset to").IsRequired();

                transferCmd.OnExecuteAsync(async (token) =>
                {
                    await TransferCommand.ExecuteAsync(
                        chainManager,
                        expressNode,
                        quantityArg.Value ?? throw new Exception(),
                        assetArg.Value ?? throw new Exception(),
                        senderArg.Value ?? throw new Exception(),
                        receiverArg.Value ?? throw new Exception(),
                        writer).ConfigureAwait(false);
                    return 0;
                });
            });

            batchCmd.Command("checkpoint", checkpointCmd =>
            {
                checkpointCmd.Command("create", checkpointCreateCmd =>
                {
                    var nameArg = checkpointCreateCmd.Argument("name", "Checkpoint file name").IsRequired();
                    var forceOpt = checkpointCmd.Option<bool>("-f|--force", "Overwrite existing data", CommandOptionType.NoValue);

                    checkpointCreateCmd.OnExecuteAsync(async (token) =>
                    {
                        await CheckpointCommand.Create.ExecuteAsync(
                            chainManager,
                            nameArg.Value ?? throw new Exception(),
                            forceOpt.ParsedValue,
                            writer).ConfigureAwait(false);
                        return 0;
                    });
                });
            });

            batchCmd.Command("contract", contractCmd =>
            {
                contractCmd.Command("deploy", contractDeployCmd =>
                {
                    var contractArg = contractDeployCmd.Argument("contract", "Path to contract .nef file").IsRequired();
                    var accountArg = contractDeployCmd.Argument("account", "Account to pay contract deployment GAS fee").IsRequired();

                    contractDeployCmd.OnExecuteAsync(async (token) =>
                    {
                        await ContractCommand.Deploy.ExecuteAsync(
                            chainManager,
                            expressNode,
                            fileSystem,
                            contractArg.Value ?? throw new Exception(),
                            accountArg.Value ?? throw new Exception(),
                            writer).ConfigureAwait(false);
                        return 0;
                    });
                });

                contractCmd.Command("invoke", contractInvokeCmd => 
                {
                    var invokeFileArg = contractInvokeCmd.Argument("invocationFile", "Path to contract invocation JSON file").IsRequired();
                    var accountArg = contractInvokeCmd.Argument("account", "Account to pay contract deployment GAS fee").IsRequired();


                });
            });
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
