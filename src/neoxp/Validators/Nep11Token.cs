// Copyright (C) 2015-2024 The Neo Project.
//
// Nep11Token.cs file belongs to neo-express project and is free
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
using Neo.SmartContract.Iterators;
using Neo.VM;
using System.Numerics;

namespace NeoExpress.Validators;

internal class Nep11Token : TokenBase
{
    public Nep11Token(
        ProtocolSettings protocolSettings,
        DataCache snapshot,
        UInt160 scriptHash) : base(protocolSettings, snapshot, scriptHash)
    {

    }

    public override bool HasValidMethods()
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "decimals");
        builder.EmitDynamicCall(ScriptHash, "symbol");
        builder.EmitDynamicCall(ScriptHash, "totalSupply");
        builder.EmitDynamicCall(ScriptHash, "balanceOf", UInt160.Zero);
        builder.EmitDynamicCall(ScriptHash, "tokensOf", UInt160.Zero);
        //builder.EmitDynamicCall(ScriptHash, "ownerOf", Array.Empty<byte>());
        builder.EmitDynamicCall(ScriptHash, "transfer", UInt160.Zero, UInt160.Zero, 0, null);
        if (Decimals > 0)
        {
            builder.EmitDynamicCall(ScriptHash, "balanceOf", UInt160.Zero, Array.Empty<byte>());
            builder.EmitDynamicCall(ScriptHash, "transfer", UInt160.Zero, UInt160.Zero, 0, Array.Empty<byte>(), null);
        }

        // You need to add this line for transaction
        // for ApplicationEngine not to crash
        // see https://github.com/neo-project/neo/issues/2952
        var tx = new Transaction() { Signers = new[] { new Signer() { Account = UInt160.Zero } }, Attributes = Array.Empty<TransactionAttribute>() };

        using var engine = builder.Invoke(_protocolSettings, _snapshot, tx);
        if (engine.State == VMState.FAULT)
            throw engine.FaultException?.InnerException! ?? engine.FaultException!;

        return engine.State == VMState.HALT;
    }

    public BigInteger BalanceOf(UInt160 owner, byte[] tokenId)
    {
        if (Decimals == 0)
            throw new InvalidOperationException();

        ArgumentNullException.ThrowIfNull(tokenId, nameof(tokenId));
        if (tokenId.Length > 64)
            throw new ArgumentOutOfRangeException(nameof(tokenId));

        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "balanceOf", owner, tokenId);

        using var appEng = builder.Invoke(_protocolSettings, _snapshot);
        if (appEng.State == VMState.HALT)
            return appEng.ResultStack.Pop().GetInteger();
        throw appEng.FaultException?.InnerException! ?? appEng.FaultException!;
    }

    public UInt160 OwnerOf(byte[] tokenId)
    {
        if (Decimals > 0)
            throw new InvalidOperationException();

        ArgumentNullException.ThrowIfNull(tokenId, nameof(tokenId));
        if (tokenId.Length > 64)
            throw new ArgumentOutOfRangeException(nameof(tokenId));

        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "ownerOf", tokenId);

        using var appEng = builder.Invoke(_protocolSettings, _snapshot);
        if (appEng.State == VMState.HALT)
            return new UInt160(appEng.ResultStack.Pop().GetSpan());
        throw appEng.FaultException?.InnerException! ?? appEng.FaultException!;
    }

    public IReadOnlyCollection<UInt160> MultiOwnerOf(byte[] tokenId)
    {
        if (Decimals == 0)
            throw new InvalidOperationException();

        ArgumentNullException.ThrowIfNull(tokenId, nameof(tokenId));
        if (tokenId.Length > 64)
            throw new ArgumentOutOfRangeException(nameof(tokenId));

        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "ownerOf", tokenId);

        using var appEng = builder.Invoke(_protocolSettings, _snapshot);
        if (appEng.State == VMState.HALT)
        {
            var result = appEng.ResultStack.Pop().GetInterface<object>();
            if (result is IIterator iter)
            {
                var refCounter = new ReferenceCounter();
                var lstOwners = new List<UInt160>();
                while (iter.Next())
                    lstOwners.Add(new UInt160(iter.Value(refCounter).GetSpan()));
                return lstOwners;
            }
            return Array.Empty<UInt160>();
        }
        throw appEng.FaultException?.InnerException! ?? appEng.FaultException!;
    }

    public IReadOnlyDictionary<string, Neo.VM.Types.StackItem> Properties(byte[] tokenId)
    {
        ArgumentNullException.ThrowIfNull(tokenId, nameof(tokenId));
        if (tokenId.Length > 64)
            throw new ArgumentOutOfRangeException(nameof(tokenId));

        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "properties", tokenId);

        using var appEng = builder.Invoke(_protocolSettings, _snapshot);
        if (appEng.State == VMState.HALT)
        {
            var result = appEng.ResultStack.Pop().GetInterface<object>();
            if (result is Neo.VM.Types.Map map)
                return map.ToDictionary(key => key.Key.GetString()!, value => value.Value);
        }
        throw appEng.FaultException?.InnerException! ?? appEng.FaultException!;
    }

    public IReadOnlyCollection<byte[]> Tokens()
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "tokens");

        using var appEng = builder.Invoke(_protocolSettings, _snapshot);
        if (appEng.State == VMState.HALT)
        {
            var result = appEng.ResultStack.Pop().GetInterface<object>();
            if (result is IIterator iter)
            {
                var refCounter = new ReferenceCounter();
                var lstTokenIds = new List<byte[]>();
                while (iter.Next())
                    lstTokenIds.Add(iter.Value(refCounter).GetSpan().ToArray());
                return lstTokenIds;
            }
            return Array.Empty<byte[]>();
        }
        throw appEng.FaultException?.InnerException! ?? appEng.FaultException!;
    }

    public IReadOnlyCollection<byte[]> TokensOf(UInt160 owner)
    {
        using var builder = new ScriptBuilder();
        builder.EmitDynamicCall(ScriptHash, "tokensOf", owner);

        using var appEng = builder.Invoke(_protocolSettings, _snapshot);
        if (appEng.State == VMState.HALT)
        {
            var result = appEng.ResultStack.Pop().GetInterface<object>();
            if (result is IIterator iter)
            {
                var refCounter = new ReferenceCounter();
                var lstTokenIds = new List<byte[]>();
                while (iter.Next())
                    lstTokenIds.Add(iter.Value(refCounter).GetSpan().ToArray());
                return lstTokenIds;
            }
            return Array.Empty<byte[]>();
        }
        throw appEng.FaultException?.InnerException! ?? appEng.FaultException!;
    }
}
