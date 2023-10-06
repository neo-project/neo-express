// Copyright (C) 2023 neo-project
//
// The neo-examples-csharp is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using OneOf.Types;
using System;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NeoWorkNet.Commands;

[Command("prefetch", Description = "Fetch data for specified contract")]
class PrefetchCommand
{
    readonly IFileSystem fs;

    public PrefetchCommand(IFileSystem fileSystem)
    {
        fs = fileSystem;
    }

    [Argument(0, Description = "Name or Hash of contract to prefetch contract storage")]
    string Contract { get; set; } = string.Empty;

    [Option("--disable-log", Description = "Disable verbose data logging")]
    bool DisableLog { get; set; }

    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
    {
        try
        {
            token = console.OverrideCancelKeyPress(token, true);

            if (!DisableLog)
            {
                var stateServiceObserver = new KeyValuePairObserver(Utility.GetDiagnosticWriter(console));
                var diagnosticObserver = new DiagnosticObserver(StateServiceStore.LoggerCategory, stateServiceObserver);
                DiagnosticListener.AllListeners.Subscribe(diagnosticObserver);
            }

            var (filename, worknet) = await fs.LoadWorknetAsync(app).ConfigureAwait(false);
            var dataDir = fs.GetWorknetDataDirectory(filename);
            if (!fs.Directory.Exists(dataDir))
                throw new Exception($"Cannot locate data directory {dataDir}");

            var contracts = worknet.BranchInfo.Contracts;
            if (!UInt160.TryParse(Contract, out var contractHash))
            {
                var info = contracts.SingleOrDefault(c => c.Name.Equals(Contract, StringComparison.OrdinalIgnoreCase));
                contractHash = info.Hash ?? UInt160.Zero;
            }

            if (contractHash == UInt160.Zero)
                throw new Exception("Invalid Contract argument");

            var contractName = contracts.SingleOrDefault(c => c.Hash == contractHash).Name;
            if (string.IsNullOrEmpty(contractName))
                throw new Exception("Invalid Contract argument");

            console.WriteLine($"Prefetching {contractName} ({contractHash}) records");

            using var db = RocksDbUtility.OpenDb(dataDir);
            using var stateStore = new StateServiceStore(worknet.Uri, worknet.BranchInfo, db);
            var result = await stateStore.PrefetchAsync(contractHash, token).ConfigureAwait(false);
            if (result.TryPickT1(out Error<string> error, out _))
            {
                throw new Exception(error.Value);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 1;
        }
        catch (Exception ex)
        {
            app.WriteException(ex);
            return 1;
        }
    }



}
