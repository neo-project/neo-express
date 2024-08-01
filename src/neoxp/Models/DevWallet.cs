// Copyright (C) 2015-2024 The Neo Project.
//
// DevWallet.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System.Collections.Immutable;

namespace NeoExpress.Models
{
    class DevWallet : Wallet
    {
        private readonly Dictionary<UInt160, DevWalletAccount> accounts = new Dictionary<UInt160, DevWalletAccount>();

        public override string Name { get; }
        public override Version? Version => null;

        public DevWallet(ProtocolSettings settings, string name, IEnumerable<DevWalletAccount>? accounts = null) : base(string.Empty, settings)
        {
            this.Name = name;

            if (accounts is not null)
            {
                foreach (var account in accounts)
                {
                    this.accounts.Add(account.ScriptHash, account);
                }
            }
        }

        public DevWallet(ProtocolSettings settings, string name, DevWalletAccount account) : base(string.Empty, settings)
        {
            this.Name = name;
            accounts.Add(account.ScriptHash, account);
        }

        public ExpressWallet ToExpressWallet() => new ExpressWallet()
        {
            Name = Name,
            Accounts = accounts.Values
                    .Select(a => a.ToExpressWalletAccount())
                    .ToList(),
        };

        public static DevWallet FromExpressWallet(ProtocolSettings settings, ExpressWallet wallet)
        {
            var accounts = wallet.Accounts.Select(a => DevWalletAccount.FromExpressWalletAccount(settings, a));
            return new DevWallet(settings, wallet.Name, accounts);
        }

        public void Export(string filename, string password)
        {
            var nep6Wallet = new NEP6Wallet(filename, password, ProtocolSettings, Name);
            foreach (var account in GetAccounts())
            {
                nep6Wallet.CreateAccount(account.Contract, account.GetKey());
            }
            nep6Wallet.Save();
        }

        public override bool Contains(UInt160 scriptHash) => accounts.ContainsKey(scriptHash);

        DevWalletAccount AddAccount(DevWalletAccount account)
        {
            lock (accounts)
            {
                accounts.Add(account.ScriptHash, account);
            }
            return account;
        }

        public override WalletAccount CreateAccount(byte[] privateKey)
        {
            var key = new KeyPair(privateKey);
            var contract = Contract.CreateSignatureContract(key.PublicKey);

            var account = new DevWalletAccount(ProtocolSettings, key, contract);
            return AddAccount(account);
        }

        public override WalletAccount CreateAccount(Contract contract, KeyPair? key = null)
        {
            var account = new DevWalletAccount(ProtocolSettings, key, contract);
            return AddAccount(account);
        }

        public override WalletAccount CreateAccount(UInt160 scriptHash)
        {
            var account = new DevWalletAccount(ProtocolSettings, null, null, scriptHash);
            return AddAccount(account);
        }

        public override bool DeleteAccount(UInt160 scriptHash) => accounts.Remove(scriptHash);

        public override WalletAccount? GetAccount(UInt160 scriptHash) => accounts.GetValueOrDefault(scriptHash);

        public override IEnumerable<WalletAccount> GetAccounts() => accounts.Values;

        public override bool VerifyPassword(string password) => true;

        public override bool ChangePassword(string oldPassword, string newPassword)
            => throw new NotImplementedException();

        public override void Delete()
        {
        }

        public override void Save()
        {
        }
    }
}
