// Copyright (C) 2015-2026 The Neo Project.
//
// FastForwardCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Persistence;
using System.ComponentModel.DataAnnotations;
using System.IO.Abstractions;

namespace NeoWorkNet.Commands;

[Command("fastfwd", Description = "Mint empty blocks to fast forward the block chain")]
class FastForwardCommand
{
    readonly IFileSystem fs;

    public FastForwardCommand(IFileSystem fs)
    {
        this.fs = fs;
    }

    [Argument(0, Description = "Number of blocks to mint")]
    [Required]
    internal uint Count { get; init; }

    [Option(Description = "Timestamp delta for last generated block")]
    internal string TimestampDelta { get; init; } = string.Empty;

    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
    {
        try
        {
            BlockProducer.ValidateCount(Count);
            var delta = BlockProducer.ParseTimestampDelta(TimestampDelta);

            var (filename, worknet) = await fs.LoadWorknetAsync(app).ConfigureAwait(false);
            var dataDir = fs.GetWorknetDataDirectory(filename);
            if (!fs.Directory.Exists(dataDir))
                throw new Exception($"Cannot locate data directory {dataDir}");

            var key = worknet.ConsensusWallet.GetAccounts().Single().GetKey()
                ?? throw new Exception("Consensus wallet is missing a private key");
            var settings = RunCommand.GetProtocolSettings(worknet);

            using var db = RocksDbUtility.OpenDb(dataDir);
            using var stateStore = new StateServiceStore(worknet.Uri, worknet.BranchInfo, db, true);
            using var trackStore = new PersistentTrackingStore(db, stateStore, true);

            BlockProducer.FastForward(trackStore, Count, delta, new[] { key }, settings);

            await console.Out.WriteLineAsync($"{Count} empty blocks minted").ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            app.WriteException(ex);
            return 1;
        }
    }
}
