// Copyright (C) 2015-2026 The Neo Project.
//
// BatchCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit;
using Neo.Wallets;
using System.ComponentModel.DataAnnotations;
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
                try
                {
                    var args = SplitCommandLine(commands.Span[i]).ToArray();
                    if (args.Length == 0
                        || args[0].StartsWith('#')
                        || args[0].StartsWith("//"))
                        continue;

                    var batchApp = new CommandLineApplication<BatchFileCommands>();
                    batchApp.UseInvariantValueParsing();
                    batchApp.Conventions.UseDefaultConventions();

                    var pr = batchApp.Parse(args);

                    // Parse() does not run DataAnnotations validation (only ExecuteAsync does),
                    // so enforce the [Required]/[AllowedValues] attributes on the batch models
                    // here. Otherwise a line missing a required argument is dispatched with the
                    // field defaulted to "" and fails later with a confusing downstream error.
                    var validationResult = pr.SelectedCommand.GetValidationResult();
                    if (validationResult != ValidationResult.Success)
                        throw new Exception(validationResult?.ErrorMessage ?? $"Invalid batch command: {commands.Span[i]}");

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
                        case CommandLineApplication<BatchFileCommands.Candidate.Register> cmd:
                            {
                                await txExec.RegisterCandidateAsync(
                                    cmd.Model.Account,
                                    cmd.Model.Password).ConfigureAwait(false);
                                break;
                            }
                        case CommandLineApplication<BatchFileCommands.Candidate.UnRegister> cmd:
                            {
                                await txExec.UnregisterCandidateAsync(
                                    cmd.Model.Account,
                                    cmd.Model.Password).ConfigureAwait(false);
                                break;
                            }
                        case CommandLineApplication<BatchFileCommands.Candidate.Vote> cmd:
                            {
                                await txExec.VoteAsync(
                                    cmd.Model.Account,
                                    cmd.Model.PublicKey,
                                    cmd.Model.Password).ConfigureAwait(false);
                                break;
                            }
                        case CommandLineApplication<BatchFileCommands.Candidate.UnVote> cmd:
                            {
                                await txExec.VoteAsync(
                                    cmd.Model.Account,
                                    null,
                                    cmd.Model.Password).ConfigureAwait(false);
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
                                if (cmd.Model.Results)
                                {
                                    await txExec.InvokeForResultsAsync(
                                        script,
                                        cmd.Model.Account,
                                        cmd.Model.WitnessScope).ConfigureAwait(false);
                                }
                                else
                                {
                                    if (string.IsNullOrEmpty(cmd.Model.Account))
                                        throw new Exception("Either Account or --results must be specified");
                                    await txExec.ContractInvokeAsync(
                                        script,
                                        cmd.Model.Account,
                                        cmd.Model.Password,
                                        cmd.Model.WitnessScope,
                                        cmd.Model.AdditionalGas).ConfigureAwait(false);
                                }
                                break;
                            }
                        case CommandLineApplication<BatchFileCommands.Contract.Run> cmd:
                            {
                                var script = await txExec.BuildInvocationScriptAsync(
                                    cmd.Model.Contract,
                                    cmd.Model.Method,
                                    cmd.Model.Arguments).ConfigureAwait(false);
                                if (cmd.Model.Results)
                                {
                                    await txExec.InvokeForResultsAsync(
                                        script,
                                        cmd.Model.Account,
                                        cmd.Model.WitnessScope).ConfigureAwait(false);
                                }
                                else
                                {
                                    if (string.IsNullOrEmpty(cmd.Model.Account))
                                        throw new Exception("Either Account or --results must be specified");
                                    await txExec.ContractInvokeAsync(
                                        script,
                                        cmd.Model.Account,
                                        cmd.Model.Password,
                                        cmd.Model.WitnessScope,
                                        cmd.Model.AdditionalGas).ConfigureAwait(false);
                                }
                                break;
                            }
                        case CommandLineApplication<BatchFileCommands.Contract.Update> cmd:
                            {
                                var data = ContractCommand.Update.ParseUpdateData(cmd.Model.Data, txExec.ContractParameterParser);
                                await txExec.ContractUpdateAsync(
                                    cmd.Model.Contract,
                                    root.Resolve(cmd.Model.NefFile),
                                    cmd.Model.Account,
                                    cmd.Model.Password,
                                    cmd.Model.WitnessScope,
                                    data).ConfigureAwait(false);
                                break;
                            }
                        case CommandLineApplication<BatchFileCommands.Execute> cmd:
                            {
                                if (string.IsNullOrEmpty(cmd.Model.Account) && !cmd.Model.Results)
                                    throw new Exception("Either Account or --results must be specified");

                                var script = ExecuteCommand.ConvertTextToScript(cmd.Model.InputText)
                                    ?? ExecuteCommand.LoadFileScript(root.Resolve(cmd.Model.InputText))
                                    ?? throw new Exception($"Invalid script: {cmd.Model.InputText}");
                                if (cmd.Model.Results)
                                {
                                    await txExec.InvokeForResultsAsync(
                                        script,
                                        cmd.Model.Account,
                                        cmd.Model.WitnessScope).ConfigureAwait(false);
                                }
                                else
                                {
                                    await txExec.ContractInvokeAsync(
                                        script,
                                        cmd.Model.Account,
                                        cmd.Model.Password,
                                        cmd.Model.WitnessScope,
                                        cmd.Model.AdditionalGas).ConfigureAwait(false);
                                }
                                break;
                            }
                        case CommandLineApplication<BatchFileCommands.FastForward> cmd:
                            {
                                FastForwardCommand.ValidateCount(cmd.Model.Count);
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
                catch (Exception ex)
                {
                    throw CreateBatchLineException(i + 1, commands.Span[i], ex);
                }
            }
        }

        internal static Exception CreateBatchLineException(int lineNumber, ReadOnlySpan<char> commandLine, Exception innerException)
            => new($"Error in batch file line {lineNumber}: \"{commandLine.Trim()}\" - {innerException.Message}", innerException);

        // SplitCommandLine method adapted from CommandLineStringSplitter class in https://github.com/dotnet/command-line-api
        internal static IEnumerable<string> SplitCommandLine(string commandLine)
        {
            var memory = commandLine.AsMemory();

            var startTokenIndex = 0;

            var pos = 0;

            var seeking = Boundary.TokenStart;
            var seekingQuote = Boundary.QuoteStart;

            while (pos < memory.Length)
            {
                var c = memory.Span[pos];

                if (char.IsWhiteSpace(c))
                {
                    if (seekingQuote == Boundary.QuoteStart)
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
                        }
                    }
                }
                else if (c == '\"')
                {
                    if (seeking == Boundary.TokenStart)
                    {
                        switch (seekingQuote)
                        {
                            case Boundary.QuoteEnd:
                                yield return CurrentToken();
                                startTokenIndex = pos;
                                seekingQuote = Boundary.QuoteStart;
                                break;

                            case Boundary.QuoteStart:
                                startTokenIndex = pos + 1;
                                seekingQuote = Boundary.QuoteEnd;
                                break;
                        }
                    }
                    else
                    {
                        switch (seekingQuote)
                        {
                            case Boundary.QuoteEnd:
                                seekingQuote = Boundary.QuoteStart;
                                break;

                            case Boundary.QuoteStart:
                                seekingQuote = Boundary.QuoteEnd;
                                break;
                        }
                    }
                }
                else if (seeking == Boundary.TokenStart && seekingQuote == Boundary.QuoteStart)
                {
                    seeking = Boundary.WordEnd;
                    startTokenIndex = pos;
                }

                Advance();

                if (IsAtEndOfInput())
                {
                    if (seekingQuote == Boundary.QuoteEnd)
                    {
                        throw new FormatException("Unbalanced quote in batch command line.");
                    }

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
                var slice = memory.Slice(startTokenIndex, IndexOfEndOfToken());
                var token = slice.ToString();
                // Mid-word quotes are stripped from the token; skip the allocation
                // for the common case of a token that contains no quote characters.
                return slice.Span.IndexOf('\"') >= 0
                    ? token.Replace("\"", string.Empty)
                    : token;
            }

            int IndexOfEndOfToken() => pos - startTokenIndex;

            bool IsAtEndOfInput() => pos == memory.Length;
        }

        private enum Boundary
        {
            TokenStart,
            WordEnd,
            QuoteStart,
            QuoteEnd
        }
    }
}
