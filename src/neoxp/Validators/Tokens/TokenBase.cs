// Copyright (C) 2015-2023 The Neo Project.
//
// The neo is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using System.Numerics;

namespace NeoExpress.Validators.Tokens;

internal abstract class TokenBase
{
    public UInt160 ScriptHash { get; private init; }
    public string Symbol { get; private set; }
    public byte Decimals { get; private set; }

    protected readonly DataCache _snapshot;
    protected readonly ProtocolSettings _protocolSettings;

    public TokenBase(
        ProtocolSettings protocolSettings,
        DataCache snapshot,
        UInt160 scriptHash)
    {
        _protocolSettings = protocolSettings;
        _snapshot = snapshot;
        ScriptHash = scriptHash;

        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "decimals");
        builder.EmitDynamicCall(ScriptHash, "symbol");

        using var engine = builder.Invoke(_protocolSettings, _snapshot);
        if (engine.State != VMState.HALT)
            throw new NotSupportedException($"{ScriptHash} is not NEP-17 compliant.");

        var results = engine.ResultStack;
        Symbol = results.Pop().GetString()!;
        Decimals = checked((byte)results.Pop().GetInteger());
    }

    public BigInteger TotalSupply()
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "totalSupply");

        using var appEng = builder.Invoke(_protocolSettings, _snapshot);
        if (appEng.State == VMState.HALT)
            return appEng.ResultStack.Pop().GetInteger();
        throw new InvalidOperationException();
    }

    public BigInteger BalanceOf(UInt160 owner)
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "balanceOf", owner);

        using var appEng = builder.Invoke(_protocolSettings, _snapshot);
        if (appEng.State == VMState.HALT)
            return appEng.ResultStack.Pop().GetInteger();
        throw new InvalidOperationException();
    }
}
