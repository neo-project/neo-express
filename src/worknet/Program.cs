using System.IO.Abstractions;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using NeoWorkNet.Commands;
using static Neo.BlockchainToolkit.Utility;

namespace NeoWorkNet;

[Command("neoxp", Description = "Neo N3 blockchain private net for developers", UsePagerForHelpText = false)]
[VersionOption(ThisAssembly.AssemblyInformationalVersion)]
[Subcommand(typeof(CreateCommand))]
class Program
{
    public static Task<int> Main(string[] args)
    {
        EnableAnsiEscapeSequences();

        var services = new ServiceCollection()
            .AddSingleton<IFileSystem, FileSystem>()
            .BuildServiceProvider();

        var app = new CommandLineApplication<Program>();
        app.Conventions
            .UseDefaultConventions()
            .UseConstructorInjection(services);

        return app.ExecuteAsync(args);
    }

    internal int OnExecute(CommandLineApplication app, IConsole console)
    {
        console.WriteLine("You must specify a subcommand.");
        app.ShowHelp(false);
        return 1;
    }

    // static async Task Main(string[] args)
    // {
    //     const string FILENAME = "./default.neo-worknet";
    //     var fs = new System.IO.Abstractions.FileSystem();
    //     var (url, branchInfo, wallet) = fs.File.Exists(FILENAME)
    //         ? await ReadWorknetAsync(fs, FILENAME).ConfigureAwait(false)
    //         : await GetWorknetAsync(Constants.MAINNET_RPC_ENDPOINTS[0], 2_000_001).ConfigureAwait(false);

    //     var stateStore = new StateServiceStore(url, branchInfo);
    //     var trackingStore = new MemoryTrackingStore(stateStore);

    //     var stateIndex = GetCurrentIndex(stateStore);
    //     var trackingIndex = GetCurrentIndex(trackingStore);
    //     if (trackingIndex == stateIndex)
    //     {
    //         CreateBranch(trackingStore, wallet.GetAccounts());
    //     }

    //     SaveWorknet(fs, FILENAME, url, branchInfo, wallet);
    // }

    // static void SaveWorknet(IFileSystem fs, string filename, string url, BranchInfo branch, ToolkitWallet wallet)
    // {
    //     using var stream = fs.File.Open(filename, FileMode.Create, FileAccess.Write);

    //     using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions() { Indented = true });
    //     writer.WriteStartObject();
    //     writer.WriteString("url", url);
    //     writer.WritePropertyName("branch-info");
    //     BranchInfoJsonConverter.WriteJson(writer, branch);
    //     writer.WritePropertyName("consensus-wallet");
    //     ToolkitWalletJsonConverter.WriteJson(writer, wallet);
    //     writer.WriteEndObject();
    // }

    // static async Task<(string url, BranchInfo branchInfo, ToolkitWallet wallet)> GetWorknetAsync(string url, uint index)
    // {
    //     var branchInfo = await BranchInfo.GetBranchInfoAsync(url, index).ConfigureAwait(false);

    //     var consensusWallet = new ToolkitWallet("consensus", branchInfo.ProtocolSettings);
    //     using (var sha = SHA256.Create())
    //     {
    //         var bytes = Encoding.UTF8.GetBytes("worknet-consensus");
    //         var hash = sha.ComputeHash(bytes);
    //         consensusWallet.CreateAccount(hash);
    //     }

    //     return (url, branchInfo, consensusWallet);
    // }

    // static async Task<(string url, BranchInfo branchInfo, ToolkitWallet wallet)> ReadWorknetAsync(IFileSystem fs, string filename)
    // {
    //     using var stream = fs.File.OpenRead(filename);
    //     using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
    //     var url = doc.RootElement.GetProperty("url").GetString() ?? throw new JsonException("url");
    //     var branchInfo = BranchInfoJsonConverter.ReadJson(doc.RootElement.GetProperty("branch-info"));
    //     var wallet = ToolkitWalletJsonConverter.ReadJson(doc.RootElement.GetProperty("consensus-wallet"), branchInfo.ProtocolSettings);
    //     return (url, branchInfo, wallet);
    // }

    // static uint GetCurrentIndex(IReadOnlyStore store)
    // {
    //     using var snapshot = new SnapshotCache(store);
    //     return NativeContract.Ledger.CurrentIndex(snapshot);
    // }

    // static void CreateBranch(IStore store, params WalletAccount[] consensusAccounts)
    //     => CreateBranch(store, consensusAccounts);


}
