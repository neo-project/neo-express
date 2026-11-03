// Copyright (C) 2015-2026 The Neo Project.
//
// Utility.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Wallets;
using NeoWorkNet.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO.Abstractions;
using static Crayon.Output;
using static Neo.BlockchainToolkit.Constants;

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

    const string LegacyDataDirectoryName = "data";

    // default worknet keeps the legacy "data" directory (backward compatible);
    // every other worknet file gets its own "<filename>.data" directory so that
    // worknets sharing a directory never share a RocksDB database
    public static string GetWorknetDataDirectory(this IFileSystem fs, string filename)
    {
        var fullPath = fs.Path.GetFullPath(filename);
        var dirname = fs.Path.GetDirectoryName(fullPath) ?? throw new Exception($"GetDirectoryName({filename}) returned null");
        var name = GetCanonicalFileName(fs, dirname, fs.Path.GetFileName(fullPath));

        if (IsDefaultWorknetName(name))
            return fs.Path.Combine(dirname, LegacyDataDirectoryName);

        return fs.Path.Combine(dirname, $"{name}.data");
    }

    // The legacy "data" directory may belong to any worknet file in the directory. Refuse to replace it
    // (via --force) when other worknet files exist, since its owner cannot be determined.
    public static void EnsureLegacyDataNotShared(this IFileSystem fs, string filename, string dataDir)
    {
        if (!fs.Directory.Exists(dataDir) || !IsDefaultWorknetName(fs.Path.GetFileName(fs.Path.GetFullPath(filename))))
            return;

        var dirname = fs.Path.GetDirectoryName(fs.Path.GetFullPath(filename))!;
        var others = fs.Directory.EnumerateFiles(dirname, "*" + WORKNET_EXTENSION)
            .Where(f => !IsDefaultWorknetName(fs.Path.GetFileName(f)))
            .ToList();
        if (others.Count > 0)
        {
            throw new Exception(
                $"{dataDir} may belong to another worknet in this directory ({fs.Path.GetFileName(others[0])}). " +
                "It will not be replaced automatically. Move or remove it manually if it is not needed.");
        }
    }

    static bool IsCaseInsensitiveFileSystem => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    static bool IsDefaultWorknetName(string name)
        => name.Equals(DEFAULT_WORKNET_FILENAME, IsCaseInsensitiveFileSystem ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    // use the on-disk casing of an existing file so that differently-cased spellings of the same
    // file on a case-insensitive filesystem resolve to the same data directory
    static string GetCanonicalFileName(IFileSystem fs, string dirname, string name)
    {
        if (!fs.Directory.Exists(dirname))
            return name;
        var existing = fs.Directory.EnumerateFiles(dirname)
            .Select(f => fs.Path.GetFileName(f))
            .Where(f => f.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return existing.Contains(name, StringComparer.Ordinal) ? name
            : IsCaseInsensitiveFileSystem && existing.Count > 0 ? existing[0]
            : name;
    }

    // Resolves the data directory of an existing worknet.
    // assumed to belong to a named worknet, since ownership cannot be determined reliably.
    public static string GetExistingWorknetDataDirectory(this IFileSystem fs, string filename)
    {
        var dataDir = fs.GetWorknetDataDirectory(filename);
        if (fs.Directory.Exists(dataDir))
            return dataDir;

        var legacyDir = fs.Path.Combine(fs.Path.GetDirectoryName(fs.Path.GetFullPath(filename))!, LegacyDataDirectoryName);
        if (!fs.Path.GetFullPath(legacyDir).Equals(fs.Path.GetFullPath(dataDir), StringComparison.Ordinal)
            && fs.Directory.Exists(legacyDir))
        {
            throw new Exception(
                $"Cannot locate data directory {dataDir}. A legacy shared data directory exists at {legacyDir}, " +
                $"but its owner cannot be determined automatically. If it belongs to {fs.Path.GetFileName(filename)}, " +
                $"stop any running worknet and manually move (not copy) it to {dataDir}.");
        }

        throw new Exception($"Cannot locate data directory {dataDir}");
    }

    public static async Task<WorknetFile> LoadWorknetAsync(this IFileSystem fs, string filename)
    {
        using var stream = fs.File.OpenRead(filename);
        using var textReader = new StreamReader(stream);
        using var reader = new JsonTextReader(textReader);
        var json = await JObject.LoadAsync(reader).ConfigureAwait(false);
        var uri = json.Value<string>("uri") ?? throw new JsonException("uri");
        var branchInfo = BranchInfo.Parse(json["branch-info"] as JObject ?? throw new JsonException("branch-info"));
        var wallet = ToolkitWallet.Parse(json["consensus-wallet"] as JObject ?? throw new JsonException("consensus-wallet"), branchInfo.ProtocolSettings);

        // older worknet files always carry an rpc-port property in their consensus-nodes
        // entry (SaveWorknetFile has written it since introduction); fall back to the
        // default port when it is missing so hand-edited files keep working
        var rpcPort = json["consensus-nodes"]?.FirstOrDefault()?["rpc-port"]?.Value<ushort>()
            ?? WorknetFile.DefaultRpcPort;
        return new WorknetFile(new Uri(uri), branchInfo, wallet) { RpcPort = rpcPort };
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
            writer.WriteProperty("rpc-port", WorknetFile.DefaultRpcPort);
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

    // Record the RPC port the node is (about to be) listening on so StopCommand can
    // reach it even when RunCommand was started with a non-default --rpc-port.
    public static void UpdateWorknetRpcPort(this IFileSystem fs, string filename, ushort rpcPort)
    {
        var json = fs.File.Exists(filename)
            ? JsonConvert.DeserializeObject<JObject>(fs.File.ReadAllText(filename))
                ?? throw new JsonException($"Could not parse {filename}")
            : throw new FileNotFoundException($"Could not find {filename}");

        var consensusNodes = json["consensus-nodes"] as JArray
            ?? throw new JsonException("consensus-nodes");
        if (consensusNodes.Count == 0)
            throw new JsonException("consensus-nodes is empty");

        consensusNodes[0]!["rpc-port"] = rpcPort;

        fs.File.WriteAllText(filename, JsonConvert.SerializeObject(json, Formatting.Indented));
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
