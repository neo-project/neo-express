// Copyright (C) 2015-2024 The Neo Project.
//
// DevWalletAccount.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Extensions;
using Neo.SmartContract;
using Neo.Wallets;

namespace NeoExpress.Models
{
    class DevWalletAccount : WalletAccount
    {
        private readonly KeyPair? key;

        public DevWalletAccount(ProtocolSettings settings, KeyPair? key, Contract? contract, UInt160 scriptHash) : base(scriptHash, settings)
        {
            this.key = key;
            Contract = contract;
        }

        public DevWalletAccount(ProtocolSettings settings, KeyPair? key, Contract contract) : base(contract.ScriptHash, settings)
        {
            this.key = key;
            Contract = contract;
        }

        public override bool HasKey => key is not null;

        public override KeyPair? GetKey() => key;

        public ExpressWalletAccount ToExpressWalletAccount() => new ExpressWalletAccount()
        {
            PrivateKey = key?.PrivateKey.ToHexString() ?? string.Empty,
            ScriptHash = ScriptHash.ToAddress(ProtocolSettings.AddressVersion),
            Label = Label,
            IsDefault = IsDefault,
            Contract = new ExpressWalletAccount.AccountContract()
            {
                Script = Contract.Script.ToHexString(),
                Parameters = Contract.ParameterList
                        .Select(p => Enum.GetName(typeof(ContractParameterType), p) ?? string.Empty)
                        .ToList()
            }
        };

        public static DevWalletAccount FromExpressWalletAccount(ProtocolSettings settings, ExpressWalletAccount account)
        {
            var keyPair = new KeyPair(account.PrivateKey.HexToBytes());
            var contract = new Contract()
            {
                Script = account.Contract?.Script.HexToBytes(),
                ParameterList = account.Contract?.Parameters
                    .Select(Enum.Parse<ContractParameterType>)
                    .ToArray()
            };

            var scriptHash = account.ScriptHash.ToScriptHash(settings.AddressVersion);

            return new DevWalletAccount(settings, keyPair, contract, scriptHash)
            {
                Label = account.Label,
                IsDefault = account.IsDefault
            };
        }
    }
}
