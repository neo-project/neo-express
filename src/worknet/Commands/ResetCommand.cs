// Copyright (C) 2015-2026 The Neo Project.
//
// ResetCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Persistence;
using System.IO.Abstractions;

namespace NeoWorkNet.Commands;

[Command("reset", Description = "Reset WorkNet back to initial branch point")]
class ResetCommand
{
    readonly IFileSystem fs;

    public ResetCommand(IFileSystem fs)
    {
        this.fs = fs;
    }

    [Option(Description = "Overwrite existing data")]
    internal bool Force { get; }

    [Option("--gas", Description = "Amount of GAS to seed the consensus account with (Default: 10000)")]
    internal decimal Gas { get; init; } = CreateCommand.DEFAULT_GAS_SEED;

    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
    {
        try
        {
            if (!Force)
                throw new InvalidOperationException("--force must be specified when resetting worknet");
            if (Gas < 0)
                throw new ArgumentException("--gas cannot be negative");

            var (filename, worknet) = await fs.LoadWorknetAsync(app).ConfigureAwait(false);
            var dataDir = fs.GetWorknetDataDirectory(filename);
            if (!fs.Directory.Exists(dataDir))
                throw new Exception($"Cannot locate data directory {dataDir}");

            using var db = RocksDbUtility.OpenDb(dataDir);
            using var stateStore = new StateServiceStore(worknet.Uri, worknet.BranchInfo, db, true);
            using var trackStore = new PersistentTrackingStore(db, stateStore, true);

            trackStore.Reset();
            CreateCommand.InitializeStore(trackStore, Gas, worknet.ConsensusWallet.GetAccounts(), worknet.BranchInfo.ProtocolSettings);
            console.WriteLine("WorkNet node reset");
            return 0;
        }
        catch (Exception ex)
        {
            app.WriteException(ex);
            return 1;
        }
    }

}
