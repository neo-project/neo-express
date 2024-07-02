// Copyright (C) 2015-2024 The Neo Project.
//
// TokenBase.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.VM;
using System.Numerics;

namespace NeoExpress.Validators;

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
            throw engine.FaultException?.InnerException! ?? engine.FaultException!;

        var results = engine.ResultStack;
        Symbol = results.Pop().GetString()!;
        Decimals = checked((byte)results.Pop().GetInteger());
    }

    public virtual bool HasValidMethods()
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "decimals");
        builder.EmitDynamicCall(ScriptHash, "symbol");
        builder.EmitDynamicCall(ScriptHash, "totalSupply");
        builder.EmitDynamicCall(ScriptHash, "balanceOf", UInt160.Zero);
        builder.EmitDynamicCall(ScriptHash, "transfer", UInt160.Zero, UInt160.Zero, 0, null);

        // You need to add this line for transaction
        // for ApplicationEngine not to crash
        // see https://github.com/neo-project/neo/issues/2952
        var tx = new Transaction() { Signers = new[] { new Signer() { Account = UInt160.Zero } }, Attributes = Array.Empty<TransactionAttribute>() };

        using var engine = builder.Invoke(_protocolSettings, _snapshot, tx);
        if (engine.State == VMState.FAULT)
            throw engine.FaultException?.InnerException! ?? engine.FaultException!;

        return engine.State == VMState.HALT;
    }

    public virtual bool IsBalanceOfValid()
    {
        return BalanceOf(UInt160.Zero) == 0;
    }

    public bool IsDecimalsValid()
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "decimals");

        byte? dec = null;
        int x = 0;

        // This loop checks for constant byte of decimals
        while (x <= 5)
        {
            using var appEng = builder.Invoke(_protocolSettings, _snapshot);
            if (appEng.State != VMState.HALT)
                throw appEng.FaultException?.InnerException! ?? appEng.FaultException!;
            try
            {
                if (dec.HasValue == false)
                    dec = (byte)appEng.ResultStack.Pop().GetInteger();
                else
                {
                    if (dec == (byte)appEng.ResultStack.Pop().GetInteger())
                        x++;
                    else
                        return false;
                }
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        return true;
    }

    public bool IsSymbolValid()
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "symbol");

        var symbol = string.Empty;
        int x = 0;

        // This loop checks for constant string of symbol
        while (x <= 5)
        {
            using var appEng = builder.Invoke(_protocolSettings, _snapshot);
            if (appEng.State != VMState.HALT)
                throw appEng.FaultException?.InnerException! ?? appEng.FaultException!;

            if (string.IsNullOrEmpty(symbol))
                symbol = appEng.ResultStack.Pop().GetString()!;
            else
            {
                if (symbol == appEng.ResultStack.Pop().GetString()!)
                    x++;
                else
                    return false;
            }
        }

        if (symbol.Any(a => char.IsWhiteSpace(a) || char.IsControl(a) || char.IsAscii(a) == false))
            return false;

        return true;
    }

    public BigInteger TotalSupply()
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "totalSupply");

        using var appEng = builder.Invoke(_protocolSettings, _snapshot);
        if (appEng.State == VMState.HALT)
            return appEng.ResultStack.Pop().GetInteger();
        throw appEng.FaultException?.InnerException! ?? appEng.FaultException!;
    }

    public BigInteger BalanceOf(UInt160 owner)
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "balanceOf", owner);

        using var appEng = builder.Invoke(_protocolSettings, _snapshot);
        if (appEng.State == VMState.HALT)
            return appEng.ResultStack.Pop().GetInteger();
        throw appEng.FaultException?.InnerException! ?? appEng.FaultException!;
    }
}
