using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using NeoWorkNet.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO.Abstractions;
using static Neo.BlockchainToolkit.Constants;
using static Crayon.Output;
using Neo.Wallets;

namespace NeoWorkNet;

static class Utility
{
    public static void WriteException(this CommandLineApplication app, Exception exception)
    {
        app.Error.WriteLine(Bright.Red($"{exception.GetType()}: {exception.Message}"));
    }

    public static async Task<(string fileName, WorknetFile file)> LoadWorknetAsync(this IFileSystem fs, CommandLineApplication app)
    {
        var option = app.GetOptions().Single(o => o.LongName == "input");
        var fileName = fs.ResolveWorkNetFileName(option.Value() ?? string.Empty);
        var worknetFile = await fs.LoadWorknetAsync(fileName).ConfigureAwait(false);
        return (fileName, worknetFile);
    }

    public static string ResolveWorkNetFileName(this IFileSystem fs, string path)
        => fs.ResolveFileName(path, WORKNET_EXTENSION, () => DEFAULT_WORKNET_FILENAME);

    public static string GetWorknetDataDirectory(this IFileSystem fs, string filename)
    {
        var dirname = fs.Path.GetDirectoryName(filename) ?? throw new Exception($"GetDirectoryName({filename}) returned null");
        return fs.Path.Combine(dirname, "data");
    }

    public static async Task<WorknetFile> LoadWorknetAsync(this IFileSystem fs, string filename)
    {
        using var stream = fs.File.OpenRead(filename);
        using var textReader = new StreamReader(stream);
        using var reader = new JsonTextReader(textReader);
        var json = await JObject.LoadAsync(reader).ConfigureAwait(false);
        var uri = json.Value<string>("uri") ?? throw new JsonException("uri");
        var branchInfo = BranchInfo.Load(json["branch-info"] as JObject ?? throw new JsonException("branch-info"));
        var wallet = ToolkitWallet.Load(json["consensus-wallet"] as JObject ?? throw new JsonException("consensus-wallet"), branchInfo.ProtocolSettings);
        return new WorknetFile(new Uri(uri), branchInfo, wallet);
    }

    public static void SaveWorknetFile(this IFileSystem fs, string filename, Uri uri, BranchInfo branch, ToolkitWallet wallet)
    {
        using var stream = fs.File.Open(filename, FileMode.Create, FileAccess.Write);
        using var textWriter = new StreamWriter(stream);
        using var writer = new JsonTextWriter(textWriter);
        writer.Formatting = Formatting.Indented;

        using var _ = writer.WriteObject();
        writer.WriteProperty("magic", branch.Network);
        writer.WriteProperty("address-version", branch.AddressVersion);
        {
            using var _1 = writer.WritePropertyArray("consensus-nodes");
            using var _2 = writer.WriteObject();
            writer.WriteProperty("rpc-port", 30332);
            using var _3 = writer.WritePropertyObject("wallet");
            writer.WriteProperty("name", "node1");
            using var _4 = writer.WritePropertyArray("accounts");
            foreach (var account in wallet.GetAccounts())
            {
                using var _5 = writer.WriteObject();
                var key = account.GetKey();
                if (key is not null) 
                {
                    writer.WriteProperty("private-key", Convert.ToHexString(key.PrivateKey).ToLower());
                }
                writer.WriteProperty("address", account.ScriptHash.ToAddress(branch.AddressVersion));
                writer.WriteProperty("is-default", account.IsDefault);
            }
        }

        writer.WriteProperty("uri", uri.ToString());
        writer.WritePropertyName("branch-info");
        branch.WriteJson(writer);
        writer.WritePropertyName("consensus-wallet");
        wallet.WriteJson(writer);
    }

    public static CancellationToken OverrideCancelKeyPress(this IConsole console, CancellationToken token, bool continueRunning = false)
    {
        var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        console.CancelKeyPress += (o, args) =>
        {
            args.Cancel = continueRunning;
            linkedTokenSource.Cancel();
        };
        return linkedTokenSource.Token;
    }

    public static Action<string, object?> GetDiagnosticWriter(IConsole console)
        => (name, value) =>
        {
            var text = value switch
            {
                GetStorageStart v => $"GetStorage for {v.ContractName} ({v.ContractHash}) with key {Convert.ToHexString(v.Key.Span)}",
                GetStorageStop v => $"GetStorage complete in {v.Elapsed}",
                DownloadStatesStart v =>
                    $"DownloadStates starting for {v.ContractName} ({(v.Prefix.HasValue ? $"{v.ContractHash} prefix {v.Prefix.Value}" : $"{v.ContractHash}")})",
                DownloadStatesStop v => $"DownloadStates complete. {v.Count} records downloaded in {v.Elapsed}",
                DownloadStatesFound v => $"DownloadStates {v.Count} records found, {v.Total} records total",
                _ => $"{name}: {value}"
            };

            console.WriteLine(Bright.Blue(text));
        };
}
