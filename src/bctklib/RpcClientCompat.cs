// Copyright (C) 2015-2026 The Neo Project.
//
// RpcClientCompat.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.Extensions.Factories;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.P2P.Payloads.Conditions;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.VM;
using Neo.Wallets;

namespace Neo.BlockchainToolkit;

// RPC client helpers for Neo 3.11.2 compatibility.
//
// Neo 3.11.2 changed Transaction.Script and NefFile.Script from ReadOnlyMemory<byte> to
// Neo.VM.Script. The published Neo.Network.RPC.RpcClient 3.10.2 package was compiled
// against the previous property signatures, so the package members listed below throw
// MissingMethodException ("Method not found") at runtime when their bodies read or write
// those properties:
//   * TransactionManagerFactory.MakeTransactionAsync - sets Transaction.Script
//   * RpcClient.GetRawTransactionAsync and RpcClient.GetBlockAsync - Utility.TransactionFromJson
//     sets Transaction.Script
//   * RpcClient.GetContractStateAsync and RpcClient.GetNativeContractsAsync - RpcNefFile.FromJson
//     sets NefFile.Script
// The helpers below replicate the 3.10.2 implementations against the Neo 3.11.2 signatures.
// ProtocolSettings is an explicit parameter because RpcClient's protocolSettings field is
// internal to the package. Remove this class once a Neo 3.11.x compatible RpcClient package
// is published.
public static class RpcClientCompat
{
    public static async Task<TransactionManager> MakeTransactionAsync(RpcClient rpcClient, ProtocolSettings protocolSettings, ReadOnlyMemory<byte> script, Signer[]? signers = null, TransactionAttribute[]? attributes = null)
    {
        RpcInvokeResult invokeResult = await rpcClient.InvokeScriptAsync(script, signers).ConfigureAwait(false);
        return await MakeTransactionAsync(rpcClient, protocolSettings, script, invokeResult.GasConsumed, signers, attributes).ConfigureAwait(false);
    }

    public static async Task<TransactionManager> MakeTransactionAsync(RpcClient rpcClient, ProtocolSettings protocolSettings, ReadOnlyMemory<byte> script, long systemFee, Signer[]? signers = null, TransactionAttribute[]? attributes = null)
    {
        uint blockCount = await rpcClient.GetBlockCountAsync().ConfigureAwait(false) - 1;

        var tx = new Transaction
        {
            Version = 0,
            Nonce = RandomNumberFactory.NextUInt32(),
            Script = new Script(script),
            Signers = signers ?? Array.Empty<Signer>(),
            ValidUntilBlock = blockCount - 1 + protocolSettings.MaxValidUntilBlockIncrement,
            SystemFee = systemFee,
            Attributes = attributes ?? Array.Empty<TransactionAttribute>(),
            Witnesses = Array.Empty<Witness>()
        };

        return new TransactionManager(tx, rpcClient);
    }

    public static async Task<Block> GetBlockAsync(RpcClient rpcClient, ProtocolSettings protocolSettings, string hashOrIndex)
    {
        var result = int.TryParse(hashOrIndex, out var index)
            ? await rpcClient.RpcSendAsync("getblock", index, true).ConfigureAwait(false)
            : await rpcClient.RpcSendAsync("getblock", hashOrIndex, true).ConfigureAwait(false);
        return BlockFromJson((JObject)result, protocolSettings);
    }

    public static async Task<RpcTransaction> GetRawTransactionAsync(RpcClient rpcClient, ProtocolSettings protocolSettings, string txHash)
    {
        var result = await rpcClient.RpcSendAsync("getrawtransaction", txHash, true).ConfigureAwait(false);
        var json = (JObject)result;
        return new RpcTransaction
        {
            Transaction = TransactionFromJson(json, protocolSettings),
            BlockHash = json["blockhash"] is null ? null : UInt256.Parse(json["blockhash"]!.AsString()),
            Confirmations = json["confirmations"] is null ? null : (uint)json["confirmations"]!.AsNumber(),
            BlockTime = json["blocktime"] is null ? null : (ulong)json["blocktime"]!.AsNumber()
        };
    }

    public static async Task<ContractState> GetContractStateAsync(RpcClient rpcClient, string hash)
    {
        var result = await rpcClient.RpcSendAsync("getcontractstate", hash).ConfigureAwait(false);
        return ContractStateFromJson((JObject)result);
    }

    public static Block BlockFromJson(JObject json, ProtocolSettings protocolSettings)
    {
        return new Block()
        {
            Header = HeaderFromJson(json, protocolSettings),
            Transactions = ((JArray)json["tx"]!).Select(p => TransactionFromJson((JObject)p!, protocolSettings)).ToArray()
        };
    }

    public static Header HeaderFromJson(JObject json, ProtocolSettings protocolSettings)
    {
        return new Header
        {
            Version = (uint)json["version"]!.AsNumber(),
            PrevHash = UInt256.Parse(json["previousblockhash"]!.AsString()),
            MerkleRoot = UInt256.Parse(json["merkleroot"]!.AsString()),
            Timestamp = (ulong)json["time"]!.AsNumber(),
            Nonce = Convert.ToUInt64(json["nonce"]!.AsString(), 16),
            Index = (uint)json["index"]!.AsNumber(),
            PrimaryIndex = (byte)json["primary"]!.AsNumber(),
            NextConsensus = json["nextconsensus"]!.ToScriptHash(protocolSettings),
            Witness = ((JArray)json["witnesses"]!).Select(p => WitnessFromJson((JObject)p!)).First()
        };
    }

    public static Transaction TransactionFromJson(JObject json, ProtocolSettings protocolSettings)
    {
        return new Transaction
        {
            Version = byte.Parse(json["version"]!.AsString()),
            Nonce = uint.Parse(json["nonce"]!.AsString()),
            Signers = ((JArray)json["signers"]!).Select(p => SignerFromJson((JObject)p!, protocolSettings)).ToArray(),
            SystemFee = long.Parse(json["sysfee"]!.AsString()),
            NetworkFee = long.Parse(json["netfee"]!.AsString()),
            ValidUntilBlock = uint.Parse(json["validuntilblock"]!.AsString()),
            Attributes = ((JArray)json["attributes"]!).Select(p => TransactionAttributeFromJson((JObject)p!)).ToArray(),
            Script = new Script(Convert.FromBase64String(json["script"]!.AsString())),
            Witnesses = ((JArray)json["witnesses"]!).Select(p => WitnessFromJson((JObject)p!)).ToArray()
        };
    }

    public static Signer SignerFromJson(JObject json, ProtocolSettings protocolSettings)
    {
        return new Signer
        {
            Account = json["account"]!.ToScriptHash(protocolSettings),
            Rules = ((JArray?)json["rules"])?.Select(p => RuleFromJson((JObject)p!, protocolSettings)).ToArray(),
            Scopes = Enum.Parse<WitnessScope>(json["scopes"]!.AsString()),
            AllowedContracts = ((JArray?)json["allowedcontracts"])?.Select(p => p!.ToScriptHash(protocolSettings)).ToArray(),
            AllowedGroups = ((JArray?)json["allowedgroups"])?.Select(p => ECPoint.Parse(p!.AsString(), ECCurve.Secp256r1)).ToArray()
        };
    }

    public static TransactionAttribute TransactionAttributeFromJson(JObject json)
    {
        TransactionAttributeType usage = Enum.Parse<TransactionAttributeType>(json["type"]!.AsString());
        return usage switch
        {
            TransactionAttributeType.HighPriority => new HighPriorityAttribute(),
            TransactionAttributeType.OracleResponse => new OracleResponse()
            {
                Id = (ulong)json["id"]!.AsNumber(),
                Code = Enum.Parse<OracleResponseCode>(json["code"]!.AsString()),
                Result = Convert.FromBase64String(json["result"]!.AsString()),
            },
            TransactionAttributeType.NotValidBefore => new NotValidBefore()
            {
                Height = (uint)json["height"]!.AsNumber(),
            },
            TransactionAttributeType.Conflicts => new Conflicts()
            {
                Hash = UInt256.Parse(json["hash"]!.AsString())
            },
            TransactionAttributeType.NotaryAssisted => new NotaryAssisted()
            {
                NKeys = (byte)json["nkeys"]!.AsNumber()
            },
            _ => throw new FormatException(),
        };
    }

    public static Witness WitnessFromJson(JObject json)
    {
        return new Witness
        {
            InvocationScript = Convert.FromBase64String(json["invocation"]!.AsString()),
            VerificationScript = Convert.FromBase64String(json["verification"]!.AsString())
        };
    }

    public static WitnessRule RuleFromJson(JObject json, ProtocolSettings protocolSettings)
    {
        return new WitnessRule()
        {
            Action = Enum.Parse<WitnessRuleAction>(json["action"]!.AsString()),
            Condition = RuleExpressionFromJson((JObject)json["condition"]!, protocolSettings)
        };
    }

    public static WitnessCondition RuleExpressionFromJson(JObject json, ProtocolSettings protocolSettings)
    {
        return json["type"]!.AsString() switch
        {
            "Or" => new OrCondition { Expressions = ((JArray)json["expressions"]!).Select(p => RuleExpressionFromJson((JObject)p!, protocolSettings)).ToArray() },
            "And" => new AndCondition { Expressions = ((JArray)json["expressions"]!).Select(p => RuleExpressionFromJson((JObject)p!, protocolSettings)).ToArray() },
            "Boolean" => new BooleanCondition { Expression = json["expression"]!.AsBoolean() },
            "Not" => new NotCondition { Expression = RuleExpressionFromJson((JObject)json["expression"]!, protocolSettings) },
            "Group" => new GroupCondition { Group = ECPoint.Parse(json["group"]!.AsString(), ECCurve.Secp256r1) },
            "CalledByContract" => new CalledByContractCondition { Hash = json["hash"]!.ToScriptHash(protocolSettings) },
            "ScriptHash" => new ScriptHashCondition { Hash = json["hash"]!.ToScriptHash(protocolSettings) },
            "CalledByEntry" => new CalledByEntryCondition(),
            "CalledByGroup" => new CalledByGroupCondition { Group = ECPoint.Parse(json["group"]!.AsString(), ECCurve.Secp256r1) },
            _ => throw new FormatException("Wrong rule's condition type"),
        };
    }

    public static ContractState ContractStateFromJson(JObject json)
    {
        return new ContractState
        {
            Id = (int)json["id"]!.AsNumber(),
            UpdateCounter = (ushort)(json["updatecounter"]?.AsNumber() ?? 0),
            Hash = UInt160.Parse(json["hash"]!.AsString()),
            Nef = NefFileFromJson((JObject)json["nef"]!),
            Manifest = ContractManifest.FromJson((JObject)json["manifest"]!)
        };
    }

    static NefFile NefFileFromJson(JObject json)
    {
        return new NefFile
        {
            Compiler = json["compiler"]!.AsString(),
            Source = json["source"]?.AsString() ?? string.Empty,
            Tokens = ((JArray)json["tokens"]!).Select(p => MethodTokenFromJson((JObject)p!)).ToArray(),
            Script = new Script(Convert.FromBase64String(json["script"]!.AsString())),
            CheckSum = (uint)json["checksum"]!.AsNumber()
        };
    }

    static MethodToken MethodTokenFromJson(JObject json)
    {
        return new MethodToken
        {
            Hash = UInt160.Parse(json["hash"]!.AsString()),
            Method = json["method"]!.AsString(),
            ParametersCount = (ushort)json["paramcount"]!.AsNumber(),
            HasReturnValue = json["hasreturnvalue"]!.AsBoolean(),
            CallFlags = Enum.Parse<CallFlags>(json["callflags"]!.AsString())
        };
    }

    static UInt160 ToScriptHash(this JToken value, ProtocolSettings protocolSettings)
    {
        var addressOrScriptHash = value.AsString() ?? throw new FormatException("Invalid address or script hash value");

        return addressOrScriptHash.Length < 40
            ? addressOrScriptHash.ToScriptHash(protocolSettings.AddressVersion)
            : UInt160.Parse(addressOrScriptHash);
    }
}
