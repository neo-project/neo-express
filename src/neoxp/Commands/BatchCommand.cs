using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit;
using NeoExpress.Node;

namespace NeoExpress.Commands
{
    [Command("batch", Description = "Execute a series of offline Neo-Express operations")]
    partial class BatchCommand
    {
        readonly IExpressChain chain;

        public BatchCommand(IExpressChain chain)
        {
            this.chain = chain;
        }

        public BatchCommand(CommandLineApplication app)
        {
            this.chain = app.GetExpressFile();
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

        internal Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken token)
            => app.ExecuteAsync(this.ExecuteAsync, token);

        internal async Task ExecuteAsync(IFileSystem fileSystem, IConsole console, CancellationToken token)
        {
            if (!fileSystem.File.Exists(BatchFile)) throw new Exception($"Batch file {BatchFile} couldn't be found");
            if (chain.IsRunning()) throw new Exception("Cannot run batch command while blockchain is running");
            var batchFileInfo = fileSystem.FileInfo.FromFileName(BatchFile);

            var commands = await fileSystem.File.ReadAllLinesAsync(BatchFile, token).ConfigureAwait(false);
            var root = batchFileInfo.Directory;

            var input = root.Resolve(string.IsNullOrEmpty(Input)
                ? Constants.DEFAULT_EXPRESS_FILENAME
                : Input);

            if (Reset.hasValue)
            {
                if (string.IsNullOrEmpty(Reset.value))
                {
                    for (int i = 0; i < chain.ConsensusNodes.Count; i++)
                    {
                        var node = chain.ConsensusNodes[i];
                        console.WriteLine($"Resetting Node {node.Wallet.Name}");
                        ResetCommand.ResetNode(fileSystem, node, true);
                    }
                }
                else
                {
                    var checkpointPath = root.Resolve(Reset.value);
                    console.WriteLine($"Restoring checkpoint {checkpointPath}");
                    CheckpointCommand.Restore.RestoreCheckpoint(chain, fileSystem, checkpointPath, true);
                }
            }

            var batchApp = new CommandLineApplication<BatchFileCommands>();
            batchApp.Conventions.UseDefaultConventions();
            using var expressNode = chain.GetExpressNode();

            for (var i = 0; i < commands.Length; i++)
            {
                var args = SplitCommandLine(commands[i]).ToArray();
                if (args.Length == 0
                    || args[0].StartsWith('#')
                    || args[0].StartsWith("//")) continue;

                var pr = batchApp.Parse(args);
                switch (pr.SelectedCommand)
                {
                    case CommandLineApplication<BatchFileCommands.Checkpoint.Create> cmd:
                        {
                            var (path, mode) = await CheckpointCommand.Create.ExecuteAsync(
                                expressNode, fileSystem,
                                root.Resolve(cmd.Model.Name),
                                cmd.Model.Force,
                                console.Out).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Contract.Deploy> cmd:
                        {
                            var (nefFile, manifest) = await fileSystem.LoadContractAsync(
                                root.Resolve(cmd.Model.Contract)).ConfigureAwait(false);
                            var (txHash, contractHash) = await ContractCommand.Deploy.ExecuteAsync(
                                expressNode,
                                nefFile,
                                manifest,
                                cmd.Model.Account,
                                cmd.Model.Password,
                                cmd.Model.WitnessScope,
                                cmd.Model.Data,
                                cmd.Model.Force,
                                console.Out).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Contract.Download> cmd:
                        {
                            if (cmd.Model.Height == 0)
                            {
                                throw new ArgumentException("Height cannot be 0. Please specify a height > 0");
                            }

                            if (chain.ConsensusNodes.Count != 1)
                            {
                                throw new ArgumentException("Contract download is only supported for single-node consensus");
                            }

                            await ContractCommand.Download.ExecuteAsync(
                                expressNode,
                                cmd.Model.Contract,
                                cmd.Model.RpcUri,
                                cmd.Model.Height,
                                cmd.Model.Force,
                                console.Out).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Contract.Invoke> cmd:
                        {
                            var script = await ContractCommand.Invoke.LoadScriptAsync(
                                expressNode, 
                                fileSystem, 
                                root.Resolve(cmd.Model.InvocationFile)).ConfigureAwait(false);
                            var txHash = await expressNode.SubmitTransactionAsync(
                                script,
                                cmd.Model.Account,
                                cmd.Model.Password,
                                cmd.Model.WitnessScope).ConfigureAwait(false);
                            console.Out.WriteTxHash(txHash, $"Invocation");
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Contract.Run> cmd:
                        {
                            var script = await expressNode.BuildInvocationScriptAsync(
                                cmd.Model.Contract,
                                cmd.Model.Method,
                                cmd.Model.Arguments).ConfigureAwait(false);
                            var txHash = await expressNode.SubmitTransactionAsync(
                                script,
                                cmd.Model.Account,
                                cmd.Model.Password,
                                cmd.Model.WitnessScope).ConfigureAwait(false);
                            console.Out.WriteTxHash(txHash, $"Invocation");
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.FastForward> cmd:
                        {
                            await FastForwardCommand.ExecuteAsync(
                                expressNode, 
                                cmd.Model.Count, 
                                cmd.Model.TimestampDelta,
                                console.Out).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Oracle.Enable> cmd:
                        {
                            await OracleCommand.Enable.ExecuteAsync(
                                expressNode, 
                                cmd.Model.Account,
                                cmd.Model.Password,
                                console.Out).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Oracle.Response> cmd:
                        {
                            var responseJson = await OracleCommand.Response.LoadResponseAsync(
                                fileSystem, 
                                root.Resolve(cmd.Model.ResponsePath)).ConfigureAwait(false);
                            var txHashes = await OracleCommand.Response.ExecuteAsync(
                                expressNode, 
                                cmd.Model.Url,
                                Neo.Network.P2P.Payloads.OracleResponseCode.Success,
                                responseJson, 
                                cmd.Model.RequestId).ConfigureAwait(false);
                            console.WriteLine($"{txHashes.Count} oracle responses submitted");
                            foreach (var txHash in txHashes) { console.WriteLine("  {txHash}"); }
                            break;
                        }
                    // case CommandLineApplication<BatchFileCommands.Policy.Block> cmd:
                    //     {
                    //         await txExec.BlockAsync(
                    //             cmd.Model.ScriptHash,
                    //             cmd.Model.Account,
                    //             cmd.Model.Password).ConfigureAwait(false);
                    //         break;
                    //     }
                    // case CommandLineApplication<BatchFileCommands.Policy.Set> cmd:
                    //     {
                    //         await txExec.SetPolicyAsync(
                    //             cmd.Model.Policy,
                    //             cmd.Model.Value,
                    //             cmd.Model.Account,
                    //             cmd.Model.Password).ConfigureAwait(false);
                    //         break;
                    //     }
                    // case CommandLineApplication<BatchFileCommands.Policy.Sync> cmd:
                    //     {
                    //         var values = await txExec.TryLoadPolicyFromFileSystemAsync(
                    //             root.Resolve(cmd.Model.Source))
                    //             .ConfigureAwait(false);
                    //         if (values.TryPickT0(out var policyValues, out _))
                    //         {
                    //             await txExec.SetPolicyAsync(policyValues, cmd.Model.Account, cmd.Model.Password);
                    //         }
                    //         else
                    //         {
                    //             throw new ArgumentException($"Could not load policy values from \"{cmd.Model.Source}\"");
                    //         }
                    //         break;
                    //     }
                    // case CommandLineApplication<BatchFileCommands.Policy.Unblock> cmd:
                    //     {
                    //         await txExec.UnblockAsync(
                    //             cmd.Model.ScriptHash,
                    //             cmd.Model.Account,
                    //             cmd.Model.Password).ConfigureAwait(false);
                    //         break;
                    //     }
                    case CommandLineApplication<BatchFileCommands.Transfer> cmd:
                        {
                            await TransferCommand.ExecuteAsync(
                                expressNode,
                                cmd.Model.Quantity,
                                cmd.Model.Asset,
                                cmd.Model.Sender,
                                cmd.Model.Password,
                                cmd.Model.Receiver, 
                                console.Out).ConfigureAwait(false);
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
