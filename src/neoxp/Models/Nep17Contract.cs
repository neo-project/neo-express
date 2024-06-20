// Copyright (C) 2015-2024 The Neo Project.
//
// Nep17Contract.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Json;
using Neo.Persistence;
using Neo.SmartContract.Native;

namespace NeoExpress.Models
{
    public struct Nep17Contract
    {
        public readonly string Symbol;
        public readonly byte Decimals;
        public readonly UInt160 ScriptHash;

        public Nep17Contract(string symbol, byte decimals, UInt160 scriptHash)
        {
            Symbol = symbol;
            Decimals = decimals;
            ScriptHash = scriptHash;
        }

        public static Nep17Contract Create<TState>(FungibleToken<TState> token)
            where TState : AccountState, new()
        {
            return new Nep17Contract(token.Symbol, token.Decimals, token.Hash);
        }

        public static Nep17Contract Unknown(UInt160 scriptHash) => new Nep17Contract("unknown", 0, scriptHash);

        public static bool TryLoad(ProtocolSettings settings, DataCache snapshot, UInt160 scriptHash, out Nep17Contract contract)
        {
            if (scriptHash == NativeContract.NEO.Hash)
            {
                contract = Nep17Contract.Create(NativeContract.NEO);
                return true;
            }

            if (scriptHash == NativeContract.GAS.Hash)
            {
                contract = Nep17Contract.Create(NativeContract.GAS);
                return true;
            }

            if (snapshot.TryGetTokenDetails(scriptHash, settings, out var details))
            {
                contract = new Nep17Contract(details.symbol, details.decimals, scriptHash);
                return true;
            }

            contract = default;
            return false;
        }

        public JObject ToJson()
        {
            var json = new JObject();
            json["scriptHash"] = ScriptHash.ToString();
            json["symbol"] = Symbol;
            json["decimals"] = Decimals;
            return json;
        }

        public static Nep17Contract FromJson(JObject json)
        {
            var symbol = json["symbol"]!.AsString();
            var scriptHash = UInt160.Parse(json["scriptHash"]!.AsString());
            var decimals = (byte)json["decimals"]!.AsNumber();
            return new Nep17Contract(symbol, decimals, scriptHash);
        }
    }
}

