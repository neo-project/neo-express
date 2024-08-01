// Copyright (C) 2015-2024 The Neo Project.
//
// ContractCommand.Download.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using NeoExpress.Node;
using System.ComponentModel.DataAnnotations;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        internal enum OverwriteForce
        {
            None,
            All,
            ContractOnly,
            StorageOnly
        }

        [Command(Name = "download", Description = "Download contract with storage from remote chain into local chain")]
        internal class Download
        {
            readonly ExpressChainManagerFactory chainManagerFactory;

            public Download(ExpressChainManagerFactory chainManagerFactory)
            {
                this.chainManagerFactory = chainManagerFactory;
            }

            [Argument(0, Description = "Contract invocation hash")]
            [Required]
            internal string Contract { get; init; } = string.Empty;

            [Argument(1, Description = "URL of Neo JSON-RPC Node\nSpecify MainNet (default), TestNet or JSON-RPC URL")]
            internal string RpcUri { get; } = string.Empty;

            [Option(Description = "Path to neo-express data file")]
            internal string Input { get; init; } = string.Empty;

            [Option(Description = "Block height to get contract state for\nZero gets the latest")]
            internal uint Height { get; } = 0;

            [Option(CommandOptionType.SingleOrNoValue,
                Description = "Replace contract and storage if it already exists\nDefaults to None if option unspecified, All if option value unspecified")]
            internal (bool hasValue, OverwriteForce value) Force { get; init; }

            internal static async Task ExecuteAsync(IExpressNode expressNode, string contract, string rpcUri, uint height, OverwriteForce force, TextWriter writer)
            {
                var (state, storage) = await NodeUtility.DownloadContractStateAsync(contract, rpcUri, height)
                    .ConfigureAwait(false);
                var storageCount = storage.Count == 1 ? "1 storage record" : $"{storage.Count} storage records";
                await expressNode.PersistContractAsync(state, storage, force).ConfigureAwait(false);
                await writer.WriteLineAsync($"{state.Manifest.Name} contract state and {storageCount} from {rpcUri} persisted successfully");
            }

            internal static OverwriteForce ParseOverwriteForceOption(CommandLineApplication app)
            {
                var forceOpt = app.Options.Single(o => o.LongName == "force");
                if (forceOpt.HasValue())
                {
                    // default to All if --force is specified without a value
                    var value = forceOpt.Value();
                    return value is null
                        ? OverwriteForce.All
                        : Enum.Parse<OverwriteForce>(value, true);
                }

                // default to None if --force is not specified
                return OverwriteForce.None;
            }

            internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
            {
                try
                {
                    var force = ParseOverwriteForceOption(app);
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);

                    if (chainManager.Chain.ConsensusNodes.Count != 1)
                    {
                        throw new ArgumentException("Contract download is only supported for single-node consensus");
                    }

                    using var expressNode = chainManager.GetExpressNode();
                    await ExecuteAsync(expressNode, Contract, RpcUri, Height, force, console.Out).ConfigureAwait(false);
                    return 0;
                }
                catch (Exception ex)
                {
                    app.WriteException(ex);
                    return 1;
                }
            }
        }
    }
}
