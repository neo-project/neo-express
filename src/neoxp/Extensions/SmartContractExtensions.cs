using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Neo;
using Neo.IO;
using Neo.Persistence;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Models;
using ByteString = Neo.VM.Types.ByteString;
using Integer = Neo.VM.Types.Integer;

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
                    if (symbol != null)
                    {
                        info = (symbol, decimals);
                        return true;
                    }
                }

                info = default;
                return false;
            }
        }

        public static bool TryGetDecimals(this DataCache snapshot, UInt160 asset, ProtocolSettings settings, out byte decimals)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(asset, "decimals");

            using var engine = builder.Invoke(settings, snapshot);
            if (engine.State != VMState.FAULT
                && engine.ResultStack.Count >= 1
                && engine.ResultStack.Pop() is Integer integer)
            {
                decimals = (byte)integer.GetInteger();
                return true;
            }

            decimals = default;
            return false;
        }

        public static bool TryGetNep17Balance(this DataCache snapshot, UInt160 asset, UInt160 address, ProtocolSettings settings, out BigInteger balance)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(asset, "balanceOf", address.ToArray());
            return TryGetBalance(snapshot, builder, settings, out balance);
        }

        public static bool TryGetDivisibleNep11Balance(this DataCache snapshot, UInt160 asset, ByteString tokenId, UInt160 address, ProtocolSettings settings, out BigInteger balance)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(asset, "balanceOf", address.ToArray(), tokenId);
            return TryGetBalance(snapshot, builder, settings, out balance);
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

        public static bool TryGetIndivisibleNep11Owner(this DataCache snapshot, UInt160 asset, ByteString tokenId, ProtocolSettings settings, out UInt160 owner)
        {
            using var builder = new ScriptBuilder();
            builder.EmitDynamicCall(asset, "ownerOf", tokenId);

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
