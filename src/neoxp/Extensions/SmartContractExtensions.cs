// Copyright (C) 2015-2024 The Neo Project.
//
// SmartContractExtensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.IO;
using Neo.Persistence;
using Neo.SmartContract.Iterators;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Models;
using System.Numerics;
using ByteString = Neo.VM.Types.ByteString;
using Integer = Neo.VM.Types.Integer;
using InteropInterface = Neo.VM.Types.InteropInterface;
using StackItemType = Neo.VM.Types.StackItemType;

namespace NeoExpress
{
    static class SmartContractExtensions
    {
        public static IEnumerable<TokenContract> EnumerateTokenContracts(this DataCache snapshot, ProtocolSettings settings)
        {
            foreach (var (contractHash, standard) in TokenContract.Enumerate(snapshot))
            {
                if (TryLoadTokenInfo(contractHash, snapshot, settings, out var info))
                {
                    yield return new TokenContract(info.symbol, info.decimals, contractHash, standard);
                }
            }

            static bool TryLoadTokenInfo(UInt160 scriptHash, DataCache snapshot, ProtocolSettings settings, out (string symbol, byte decimals) info)
            {
                if (scriptHash == NativeContract.NEO.Hash)
                {
                    info = (NativeContract.NEO.Symbol, NativeContract.NEO.Decimals);
                    return true;
                }

                if (scriptHash == NativeContract.GAS.Hash)
                {
                    info = (NativeContract.GAS.Symbol, NativeContract.GAS.Decimals);
                    return true;
                }

                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(scriptHash, "symbol");
                builder.EmitDynamicCall(scriptHash, "decimals");
                using var engine = builder.Invoke(settings, snapshot);
                if (engine.State != VMState.FAULT && engine.ResultStack.Count == 2)
                {
                    var decimals = (byte)engine.ResultStack.Pop().GetInteger();
                    var symbol = engine.ResultStack.Pop().GetString();
                    if (symbol is not null)
                    {
                        info = (symbol, decimals);
                        return true;
                    }
                }

                info = default;
                return false;
            }
        }

        public static bool TryGetTokenDetails(this DataCache snapshot, UInt160 asset, ProtocolSettings settings, out (string name, string symbol, byte decimals) details)
        {
            var contractState = NativeContract.ContractManagement.GetContract(snapshot, asset);
            if (contractState is not null)
            {
                using var builder = new ScriptBuilder();
                builder.EmitDynamicCall(asset, "symbol");
                builder.EmitDynamicCall(asset, "decimals");

                using var engine = builder.Invoke(settings, snapshot);
                if (engine.State != VMState.FAULT && engine.ResultStack.Count >= 2)
                {
                    var decimals = (byte)engine.ResultStack.Pop<Neo.VM.Types.Integer>().GetInteger();
                    var symbol = System.Text.Encoding.UTF8.GetString(engine.ResultStack.Pop().GetSpan());
                    details = (contractState.Manifest.Name, symbol, decimals);
                    return true;
                }
            }
            details = default;
            return false;
        }

        public static BigInteger GetNep17Balance(this DataCache snapshot, UInt160 asset, UInt160 address, ProtocolSettings settings)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(asset, "balanceOf", address.ToArray());
            return TryGetBalance(snapshot, builder, settings, out var balance) ? balance : BigInteger.Zero;
        }

        public static IEnumerable<ReadOnlyMemory<byte>> GetNep11Tokens(this DataCache snapshot, UInt160 asset, UInt160 address, ProtocolSettings settings)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(asset, "tokensOf", address.ToArray());
            using var engine = builder.Invoke(settings, snapshot);
            if (engine.State != VMState.FAULT
                && engine.ResultStack.Count >= 1
                && engine.ResultStack.Pop() is InteropInterface interop
                && interop.GetInterface<object>() is IIterator iterator)
            {
                while (iterator.Next())
                {
                    var value = iterator.Value(null);
                    var byteString = value.Type == StackItemType.ByteString
                        ? (ByteString)value
                        : (ByteString)(value.ConvertTo(StackItemType.ByteString));

                    yield return (ReadOnlyMemory<byte>)byteString;
                }
            }
        }

        public static BigInteger GetDivisibleNep11Balance(this DataCache snapshot, UInt160 asset, ReadOnlyMemory<byte> tokenId, UInt160 address, ProtocolSettings settings)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(asset, "balanceOf", address.ToArray(), (ByteString)tokenId);
            return TryGetBalance(snapshot, builder, settings, out var balance)
                ? balance
                : BigInteger.Zero;
        }

        static bool TryGetBalance(DataCache snapshot, ScriptBuilder builder, ProtocolSettings settings, out BigInteger balance)
        {
            using var engine = builder.Invoke(settings, snapshot);
            if (engine.State != VMState.FAULT
                && engine.ResultStack.Count >= 1
                && engine.ResultStack.Pop() is Integer integer)
            {
                balance = integer.GetInteger();
                return true;
            }

            balance = default;
            return false;
        }


        public static UInt160 GetIndivisibleNep11Owner(this DataCache snapshot, UInt160 asset, ReadOnlyMemory<byte> tokenId, ProtocolSettings settings)
            => TryGetIndivisibleNep11Owner(snapshot, asset, tokenId, settings, out var owner) ? owner : UInt160.Zero;

        public static bool TryGetIndivisibleNep11Owner(this DataCache snapshot, UInt160 asset, ReadOnlyMemory<byte> tokenId, ProtocolSettings settings, out UInt160 owner)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(asset, "ownerOf", (ByteString)tokenId);

            using var engine = builder.Invoke(settings, snapshot);
            if (engine.State != VMState.FAULT
                && engine.ResultStack.Count >= 1
                && engine.ResultStack.Pop() is ByteString byteString
                && byteString.Size == UInt160.Length)
            {
                owner = new UInt160(engine.ResultStack.Pop().GetSpan());
                return true;
            }

            owner = UInt160.Zero;
            return false;
        }
    }
}
