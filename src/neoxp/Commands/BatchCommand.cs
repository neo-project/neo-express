// Copyright (C) 2015-2024 The Neo Project.
//
// BatchCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.Wallets;
using System.IO.Abstractions;
using static Neo.BlockchainToolkit.Constants;

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

        [Argument(0, Description = $"Path to {EXPRESS_BATCH_EXTENSION} file to run")]
        internal string BatchFile { get; init; } = string.Empty;

        [Option("-r|--reset[:<CHECKPOINT>]", CommandOptionType.SingleOrNoValue,
            Description = "Reset blockchain to genesis or specified checkpoint before running batch file commands")]
        internal (bool hasValue, string value) Reset { get; init; }

        [Option(Description = "Enable contract execution tracing")]
        internal bool Trace { get; init; } = false;

        [Option(Description = $"Path to {EXPRESS_EXTENSION} data file")]
        internal string Input { get; init; } = string.Empty;

        internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
        {
            try
            {
                var batchFilename = fileSystem.ResolveFileName(BatchFile, EXPRESS_BATCH_EXTENSION, () => DEFAULT_BATCH_FILENAME);

                if (!fileSystem.File.Exists(batchFilename))
                    throw new Exception($"Batch file \"{batchFilename}\" could not be found");
                var batchFileInfo = fileSystem.FileInfo.New(batchFilename);
                var batchDirInfo = batchFileInfo.Directory ?? throw new InvalidOperationException("batchFileInfo.Directory is null");

                var commands = await fileSystem.File.ReadAllLinesAsync(batchFilename, token).ConfigureAwait(false);
                await ExecuteAsync(batchDirInfo, commands, console.Out).ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex)
            {
                app.WriteException(ex, showInnerExceptions: true);
                return 1;
            }
        }

        internal async Task ExecuteAsync(IDirectoryInfo root, ReadOnlyMemory<string> commands, System.IO.TextWriter writer, ExpressChainManager? chainManager = null, string? chainFilename = null)
        {
            if (chainManager == null)
                (chainManager, chainFilename) = chainManagerFactory.LoadChain(Input);

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
                    var checkpoint = root.Resolve(Reset.value);
                    await writer.WriteLineAsync($"Restoring checkpoint {checkpoint}");
                    chainManager.RestoreCheckpoint(checkpoint, true);
                }
            }

            using var txExec = txExecutorFactory.Create(chainManager, Trace, false);

            for (var i = 0; i < commands.Length; i++)
            {
                var batchApp = new CommandLineApplication<BatchFileCommands>();
                batchApp.Conventions.UseDefaultConventions();

                var args = SplitCommandLine(commands.Span[i]).ToArray();
                if (args.Length == 0
                    || args[0].StartsWith('#')
                    || args[0].StartsWith("//"))
                    continue;

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
                                root.Resolve(cmd.Model.Contract),
                                cmd.Model.Account,
                                cmd.Model.Password,
                                cmd.Model.WitnessScope,
                                cmd.Model.Data,
                                cmd.Model.Force).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Contract.Download> cmd:
                        {
                            if (cmd.Model.Height == 0)
                            {
                                throw new ArgumentException("Height cannot be 0. Please specify a height > 0");
                            }

                            if (chainManager.Chain.ConsensusNodes.Count != 1)
                            {
                                throw new ArgumentException("Contract download is only supported for single-node consensus");
                            }

                            var force = ContractCommand.Download.ParseOverwriteForceOption(cmd);

                            await ContractCommand.Download.ExecuteAsync(
                                txExec.ExpressNode,
                                cmd.Model.Contract,
                                cmd.Model.RpcUri,
                                cmd.Model.Height,
                                force,
                                writer).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Contract.Invoke> cmd:
                        {
                            var script = await txExec.LoadInvocationScriptAsync(
                                root.Resolve(cmd.Model.InvocationFile)).ConfigureAwait(false);
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
                    case CommandLineApplication<BatchFileCommands.Contract.Update> cmd:
                        {
                            await txExec.ContractUpdateAsync(
                                cmd.Model.Contract,
                                cmd.Model.NefFile,
                                cmd.Model.Account,
                                cmd.Model.Password,
                                cmd.Model.WitnessScope).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.FastForward> cmd:
                        {
                            var timestampDelta = FastForwardCommand.ParseTimestampDelta(cmd.Model.TimestampDelta);
                            await txExec.ExpressNode.FastForwardAsync(
                                cmd.Model.Count,
                                timestampDelta).ConfigureAwait(false);
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
                                root.Resolve(cmd.Model.ResponsePath),
                                cmd.Model.RequestId).ConfigureAwait(false);
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
                    case CommandLineApplication<BatchFileCommands.Policy.Sync> cmd:
                        {
                            if (string.IsNullOrEmpty(cmd.Model.Account))
                                throw new ArgumentException("Policy sync requires --account field");

                            var values = await txExec.TryGetRemoteNetworkPolicyAsync(cmd.Model.Source).ConfigureAwait(false);

                            if (values.IsT1)
                                values = await txExec.TryLoadPolicyFromFileSystemAsync(cmd.Model.Source)
                                    .ConfigureAwait(false);

                            if (values.TryPickT0(out var policyValues, out _))
                            {
                                await txExec.SetPolicyAsync(policyValues, cmd.Model.Account, cmd.Model.Password);
                            }
                            else
                            {
                                throw new ArgumentException($"Could not load policy values from \"{cmd.Model.Source}\"");
                            }
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
                                cmd.Model.Receiver,
                                cmd.Model.Data).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.TransferNFT> cmd:
                        {
                            await txExec.TransferNFTAsync(
                                cmd.Model.Contract,
                                cmd.Model.TokenId,
                                cmd.Model.Sender,
                                cmd.Model.Password,
                                cmd.Model.Receiver,
                                cmd.Model.Data).ConfigureAwait(false);
                            break;
                        }
                    case CommandLineApplication<BatchFileCommands.Wallet.Create> cmd:
                        {
                            var wallet = chainManager.CreateWallet(
                                cmd.Model.Name,
                                cmd.Model.PrivateKey,
                                cmd.Model.Force);
                            chainManager.SaveChain(chainFilename!);
                            await writer.WriteLineAsync($"Created Wallet {cmd.Model.Name}");
                            for (int x = 0; x < wallet.Accounts.Count; x++)
                                await writer.WriteLineAsync($"    Address: {wallet.Accounts[x].ScriptHash}");
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
