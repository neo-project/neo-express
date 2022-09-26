using System.Diagnostics;
using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit.Persistence;
using OneOf.Types;

namespace NeoWorkNet.Commands;

[Command("prefetch", Description = "")]
class PrefetchCommand
{
    readonly IFileSystem fs;

    public PrefetchCommand(IFileSystem fileSystem)
    {
        fs = fileSystem;
    }

    [Argument(0)]
    string Contract { get; set; } = string.Empty;



    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console, CancellationToken token)
    {
        try
        {
            token = console.OverrideCancelKeyPress(token, true);

            var stateServiceObserver = new KeyValuePairObserver(OnStateStoreDiagnostic);
            var diagnosticObserver = new DiagnosticObserver(StateServiceStore.LoggerCategory, stateServiceObserver);
            DiagnosticListener.AllListeners.Subscribe(diagnosticObserver);

            var (fileName, worknet) = await fs.LoadWorknetAsync(app).ConfigureAwait(false);
            var dataDir = fs.Path.Combine(fs.Path.GetDirectoryName(fileName), "data");
            if (!fs.Directory.Exists(dataDir)) throw new Exception($"Cannot locate data directory {dataDir}");

            if (!UInt160.TryParse(Contract, out var contractHash))
            {
                contractHash = UInt160.Zero;
                foreach (var info in worknet.BranchInfo.Contracts)
                {
                    if (string.Equals(info.Name, Contract, StringComparison.OrdinalIgnoreCase))
                    {
                        contractHash = info.Hash;
                    }
                }
            }
            if (contractHash == UInt160.Zero)
            {
                throw new Exception("Invalid Contract argument");
            }

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
            await app.Error.WriteLineAsync(ex.Message);
            return 1;
        }

        void OnStateStoreDiagnostic(string name, object? value)
        {
            console.WriteLine($"{name}: {value}");
        }
    }
}
