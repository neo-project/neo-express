// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit.Persistence;
using System;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
    {
        try
        {
            if (!Force)
                throw new InvalidOperationException("--force must be specified when resetting worknet");

            var (filename, worknet) = await fs.LoadWorknetAsync(app).ConfigureAwait(false);
            var dataDir = fs.GetWorknetDataDirectory(filename);
            if (!fs.Directory.Exists(dataDir))
                throw new Exception($"Cannot locate data directory {dataDir}");

            using var db = RocksDbUtility.OpenDb(dataDir);
            using var stateStore = new StateServiceStore(worknet.Uri, worknet.BranchInfo, db, true);
            using var trackStore = new PersistentTrackingStore(db, stateStore, true);

            trackStore.Reset();
            CreateCommand.InitializeStore(trackStore, worknet.ConsensusWallet.GetAccounts().Single());
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
