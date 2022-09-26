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

    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
    {
        try
        {
            var (fileName, url, branchInfo, wallet) = await fs.LoadWorknetAsync(app).ConfigureAwait(false);
            var dataDir = fs.Path.Combine(fs.Path.GetDirectoryName(fileName), "data");
            if (!fs.Directory.Exists(dataDir)) throw new Exception($"Cannot locate data directory {dataDir}");

            if (!UInt160.TryParse(Contract, out var contractHash))
            {
                contractHash = UInt160.Zero;
                foreach (var info in branchInfo.Contracts)
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
            using var stateStore = new StateServiceStore(url, branchInfo, db);
            var result = await stateStore.PrefetchAsync(contractHash).ConfigureAwait(false);
            if (result.TryPickT1(out Error<string> error, out _))
            {
                throw new Exception(error.Value);
            }

            return 0;
        }
        catch (Exception ex)
        {
            await app.Error.WriteLineAsync(ex.Message);
            return 1;
        }
    }
}
