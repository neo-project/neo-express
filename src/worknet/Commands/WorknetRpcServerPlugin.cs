// Copyright (C) 2015-2026 The Neo Project.
//
// WorknetRpcServerPlugin.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.Plugins;
using Neo.Extensions;
using Neo.Json;
using Neo.Network.RPC;
using Neo.Plugins;
using Neo.Plugins.RpcServer;
using Neo.Plugins.RpcServer.Model;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;

namespace NeoWorkNet.Commands;

class WorknetRpcServerPlugin : Plugin
{
    readonly RpcServersSettings settings;
    readonly ToolkitPersistencePlugin persistencePlugin;
    readonly RpcClient rpcClient;
    readonly CancellationTokenSource cancellationToken = new();
    NeoSystem? neoSystem;
    RpcServer? rpcServer;
    RpcServerCompat? rpcServerCompat;

    public CancellationToken CancellationToken => cancellationToken.Token;

    public WorknetRpcServerPlugin(RpcServersSettings settings, ToolkitPersistencePlugin persistencePlugin, Uri uri)
    {
        this.settings = settings;
        this.persistencePlugin = persistencePlugin;
        this.rpcClient = new RpcClient(uri);
    }

    protected override void OnSystemLoaded(NeoSystem system)
    {
        if (neoSystem is not null)
            throw new Exception($"{nameof(OnSystemLoaded)} already called");
        neoSystem = system;
        rpcServer = new RpcServer(system, settings);
        rpcServerCompat = new RpcServerCompat(system, settings);
        rpcServer.RegisterMethods(this);
        rpcServer.StartRpcServer();
        base.OnSystemLoaded(system);
    }

    public override void Dispose()
    {
        rpcServerCompat?.Dispose();
        rpcServer?.Dispose();
        rpcClient.Dispose();
        cancellationToken.Dispose();
        base.Dispose();
    }

    [RpcMethod]
    public JObject ExpressShutdown(JArray _)
    {
        const int SHUTDOWN_TIME = 2;

        var proc = System.Diagnostics.Process.GetCurrentProcess();
        var response = new JObject();
        response["processId"] = proc.Id;

        Logs.GetLogger(nameof(WorknetRpcServerPlugin))
            .Information("ExpressShutdown requested. Shutting down in {ShutdownTime} seconds", SHUTDOWN_TIME);
        cancellationToken.CancelAfter(TimeSpan.FromSeconds(SHUTDOWN_TIME));
        return response;
    }

    [RpcMethod]
    public JArray ExpressListContracts(JArray _)
    {
        if (neoSystem is null)
            throw new NullReferenceException(nameof(neoSystem));
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
        UInt256 hash = UInt256.Parse(RequiredStringParam(@params, 0));
        var log = persistencePlugin.GetAppLog(hash);
        return log is not null
            ? log
            : rpcClient.RpcSend(RpcClient.GetRpcName(), $"{hash}");
    }

    [RpcMethod]
    public JObject GetNep17Balances(JArray @params)
    {
        if (neoSystem is null)
            throw new NullReferenceException(nameof(neoSystem));
        var address = ParseScriptHash(RequiredStringParam(@params, 0), neoSystem.Settings);
        using var snapshot = neoSystem.GetSnapshotCache();

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
        if (neoSystem is null)
            throw new NullReferenceException(nameof(neoSystem));
        var address = ParseScriptHash(RequiredStringParam(@params, 0), neoSystem.Settings);
        using var snapshot = neoSystem.GetSnapshotCache();
        var balances = ToolkitRpcServer.GetNep11Balances(snapshot, persistencePlugin, address, neoSystem.Settings);

        var jsonBalances = new JArray();
        foreach (var balance in balances)
        {
            jsonBalances.Add(CreateNep11BalanceJson(balance));
        }

        return new JObject
        {
            ["address"] = Neo.Wallets.Helper.ToAddress(address, neoSystem.Settings.AddressVersion),
            ["balance"] = jsonBalances
        };
    }

    internal static JObject CreateNep11BalanceJson(ToolkitRpcServer.Nep11Balance balance)
    {
        var jsonTokens = new JArray();
        for (int i = 0; i < balance.Tokens.Count; i++)
        {
            var token = balance.Tokens[i];
            jsonTokens.Add(new JObject
            {
                ["tokenid"] = token.TokenId.Span.ToHexString(),
                ["amount"] = $"{token.Balance}",
                ["lastupdatedblock"] = token.LastUpdatedBlock,
            });
        }

        return new JObject
        {
            ["assethash"] = $"{balance.AssetHash}",
            ["name"] = balance.Name,
            ["symbol"] = balance.Symbol,
            ["decimals"] = $"{balance.Decimals}",
            ["tokens"] = jsonTokens,
        };
    }

    [RpcMethod]
    public JToken GetNep11Properties(JArray @params)
    {
        if (neoSystem is null)
            throw new NullReferenceException(nameof(neoSystem));

        var contractHash = ParseScriptHash(RequiredStringParam(@params, 0), neoSystem.Settings);
        var tokenId = RequiredStringParam(@params, 1).HexToBytes();
        using var snapshot = neoSystem.GetSnapshotCache();

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

    // Validate a required string parameter before indexing the attacker-controlled
    // @params array. Without this a too-short array throws ArgumentOutOfRangeException
    // and a JSON null throws NullReferenceException, both of which the RpcServer
    // surfaces as a non-standard error that leaks the internal exception message
    // instead of a clean "Invalid params" (-32602), unlike the Express plugin.
    static string RequiredStringParam(JArray @params, int index)
    {
        if (index < 0 || index >= @params.Count || @params[index] is null)
            throw new Neo.Network.RPC.RpcException(-32602, "Invalid params");
        return @params[index]!.AsString();
    }

    // The Neo.Plugins.RpcServer 3.10.2 invoke handlers construct Transactions with the
    // ReadOnlyMemory<byte>-typed Script property that Neo 3.11.2 replaced with Neo.VM.Script,
    // so every invokescript/invokefunction/invokecontractverify request fails with
    // MissingMethodException before the requested script ever runs. RpcServer.RegisterMethods
    // overwrites same-named methods, so these handlers substitute 3.11.2-clean
    // implementations (see RpcServerCompat). They can be removed once a Neo 3.11.x
    // compatible Neo.Plugins.RpcServer package is published.
    [RpcMethod]
    public JToken InvokeScript(byte[] script, SignersAndWitnesses signersAndWitnesses = default, bool useDiagnostic = false)
    {
        var (signers, witnesses) = signersAndWitnesses;
        return rpcServerCompat!.GetInvokeResult(script, signers, witnesses, useDiagnostic);
    }

    [RpcMethod]
    public JToken InvokeFunction(UInt160 scriptHash, string operation,
        ContractParameter[]? args = null, SignersAndWitnesses signersAndWitnesses = default, bool useDiagnostic = false)
    {
        var (signers, witnesses) = signersAndWitnesses;
        byte[] script;
        using (var sb = new ScriptBuilder())
        {
            script = sb.EmitDynamicCall(scriptHash, operation, args ?? []).ToArray();
        }
        return rpcServerCompat!.GetInvokeResult(script, signers, witnesses, useDiagnostic);
    }

    [RpcMethod]
    public JToken InvokeContractVerify(UInt160 scriptHash, ContractParameter[]? args = null,
        SignersAndWitnesses signersAndWitnesses = default)
    {
        var (signers, witnesses) = signersAndWitnesses;
        return rpcServerCompat!.GetVerificationResult(scriptHash, args ?? [], signers, witnesses);
    }

    [RpcMethod]
    public JToken TraverseIterator(Guid sessionId, Guid iteratorId, int count)
        => rpcServerCompat!.TraverseIterator(sessionId, iteratorId, count);

    [RpcMethod]
    public JToken TerminateSession(Guid sessionId)
        => rpcServerCompat!.TerminateSession(sessionId);
}
