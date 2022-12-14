using Neo;
using Neo.BlockchainToolkit.Plugins;
using Neo.Json;
using Neo.Network.RPC;
using Neo.Plugins;
using Neo.SmartContract.Native;

namespace NeoWorkNet.Commands;

class WorknetRpcServerPlugin : Plugin
{
    readonly RpcServerSettings settings;
    readonly ToolkitPersistencePlugin persistencePlugin;
    readonly RpcClient rpcClient;
    readonly CancellationTokenSource cancellationToken = new();
    NeoSystem? neoSystem;
    RpcServer? rpcServer;

    public CancellationToken CancellationToken => cancellationToken.Token;

    public WorknetRpcServerPlugin(RpcServerSettings settings, ToolkitPersistencePlugin persistencePlugin, Uri uri)
    {
        this.settings = settings;
        this.persistencePlugin = persistencePlugin;
        this.rpcClient = new RpcClient(uri);
    }

    protected override void OnSystemLoaded(NeoSystem system)
    {
        if (neoSystem is not null) throw new Exception($"{nameof(OnSystemLoaded)} already called");
        neoSystem = system;
        rpcServer = new RpcServer(system, settings);
        rpcServer.RegisterMethods(this);
        rpcServer.StartRpcServer();
        base.OnSystemLoaded(system);
    }

    public override void Dispose()
    {
        rpcServer?.Dispose();
        base.Dispose();
    }

    [RpcMethod]
    public JObject ExpressShutdown(JArray _)
    {
        const int SHUTDOWN_TIME = 2;

        var proc = System.Diagnostics.Process.GetCurrentProcess();
        var response = new JObject();
        response["process-id"] = proc.Id;

        Neo.Utility.Log(nameof(WorknetRpcServerPlugin), LogLevel.Info, $"ExpressShutdown requested. Shutting down in {SHUTDOWN_TIME} seconds");
        cancellationToken.CancelAfter(TimeSpan.FromSeconds(SHUTDOWN_TIME));
        return response;
    }

    [RpcMethod]
    public JArray ExpressListContracts(JArray _)
    {
        if (neoSystem is null) throw new NullReferenceException(nameof(neoSystem));
        var contracts = NativeContract.ContractManagement.ListContracts(neoSystem.StoreView)
            .OrderBy(c => c.Id);

        var json = new JArray();
        foreach (var contract in contracts)
        {
            var jsonContract = new JObject();
            jsonContract["hash"] = contract.Hash.ToString();
            jsonContract["manifest"] = contract.Manifest.ToJson();
            json.Add(jsonContract);
        }
        return json;
    }

    [RpcMethod]
    public JToken GetApplicationLog(JArray @params)
    {
        UInt256 hash = UInt256.Parse(@params[0]!.AsString());
        var log = persistencePlugin.GetAppLog(hash);
        return log is not null
            ? log
            : rpcClient.RpcSend(RpcClient.GetRpcName(), $"{hash}");
    }

    [RpcMethod]
    public JObject GetNep17Balances(JArray @params)
    {
        if (neoSystem is null) throw new NullReferenceException(nameof(neoSystem));
        var address = ParseScriptHash(@params[0]!.AsString(), neoSystem.Settings);
        using var snapshot = neoSystem.GetSnapshot();

        var balances = ToolkitRpcServer.GetNep17Balances(snapshot, persistencePlugin, address, neoSystem.Settings);

        var jsonBalances = new JArray();
        foreach (var balance in balances)
        {
            jsonBalances.Add(new JObject
            {
                ["assethash"] = $"{balance.AssetHash}",
                ["name"] = balance.Name,
                ["symbol"] = balance.Symbol,
                ["decimals"] = $"{balance.Decimals}",
                ["amount"] = $"{balance.Balance}",
                ["lastupdatedblock"] = balance.LastUpdatedBlock
            });
        }

        return new JObject
        {
            ["address"] = Neo.Wallets.Helper.ToAddress(address, neoSystem.Settings.AddressVersion),
            ["balance"] = jsonBalances
        };
    }

    [RpcMethod]
    public JObject GetNep11Balances(JArray @params)
    {
        if (neoSystem is null) throw new NullReferenceException(nameof(neoSystem));
        var address = ParseScriptHash(@params[0]!.AsString(), neoSystem.Settings);
        using var snapshot = neoSystem.GetSnapshot();
        var balances = ToolkitRpcServer.GetNep11Balances(snapshot, persistencePlugin, address, neoSystem.Settings);

        var jsonBalances = new JArray();
        foreach (var balance in balances)
        {
            var jsonTokens = new JArray();
            for (int i = 0; i < balance.Tokens.Count; i++)
            {
                var token = balance.Tokens[i];
                jsonTokens.Add(new JObject
                {
                    ["tokenid"] = Convert.ToHexString(token.TokenId.Span),
                    ["amount"] = $"{token.Balance}",
                    ["lastupdatedblock"] = token.LastUpdatedBlock,
                });
            }

            jsonBalances.Add(new JObject
            {
                ["assethash"] = $"{balance.AssetHash}",
                ["name"] = balance.Name,
                ["symbol"] = balance.Symbol,
                ["decimals"] = $"{balance.Decimals}",
                ["token"] = jsonTokens,
            });
        }

        return new JObject
        {
            ["address"] = Neo.Wallets.Helper.ToAddress(address, neoSystem.Settings.AddressVersion),
            ["balance"] = jsonBalances
        };
    }

    [RpcMethod]
    public JToken GetNep11Properties(JArray @params)
    {
        if (neoSystem is null) throw new NullReferenceException(nameof(neoSystem));

        var contractHash = ParseScriptHash(@params[0]!.AsString(), neoSystem.Settings);
        var tokenId = @params[1]!.AsString().HexToBytes();
        using var snapshot = neoSystem.GetSnapshot();

        var properties = ToolkitRpcServer.GetNep11Properties(snapshot, contractHash, tokenId.AsMemory(), neoSystem.Settings);
        JObject json = new();
        foreach (var (key, value) in properties)
        {
            json[key] = ToolkitRpcServer.Nep11PropertyNames.Contains(key)
                ? value.GetString()
                : value.IsNull
                    ? null
                    : Convert.ToBase64String(value.GetSpan());
        }
        return json;
    }

    static UInt160 ParseScriptHash(string text, ProtocolSettings settings)
        => text.Length < 40
            ? Neo.Wallets.Helper.ToScriptHash(text, settings.AddressVersion)
            : UInt160.Parse(text);
}
