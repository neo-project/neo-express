using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.SmartContract;
using Neo.Wallets;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Wallets.NEP6;

namespace NeoExpress.Models
{
    class DevWallet : Wallet
    {
        private readonly Dictionary<UInt160, DevWalletAccount> accounts = new Dictionary<UInt160, DevWalletAccount>();

        public override string Name { get; }
        public override Version? Version => null;

        public DevWallet(string name, byte addressVersion, IEnumerable<DevWalletAccount>? accounts = null)
            : this(name, GetProtocolSettings(addressVersion), accounts)
        {
        }

        public DevWallet(string name, ProtocolSettings settings, IEnumerable<DevWalletAccount>? accounts = null)
            : base(string.Empty, settings)
        {
            this.Name = name;

            if (accounts != null)
            {
                foreach (var account in accounts)
                {
                    this.accounts.Add(account.ScriptHash, account);
                }
            }
        }

        public DevWallet(string name, byte addressVersion, DevWalletAccount account) 
            : base(string.Empty, GetProtocolSettings(addressVersion))
        {
            this.Name = name;
            accounts.Add(account.ScriptHash, account);
        }

        public static ProtocolSettings GetProtocolSettings(byte addressVersion)
            => addressVersion == ProtocolSettings.Default.AddressVersion
                ? ProtocolSettings.Default
                : ProtocolSettings.Default with { AddressVersion = addressVersion };

        public ExpressWallet ToExpressWallet() => new ExpressWallet()
        {
            Name = Name,
            Accounts = accounts.Values
                    .Select(a => a.ToExpressWalletAccount())
                    .ToList(),
        };

        public static DevWallet FromExpressWallet(ExpressWallet wallet, byte addressVersion)
        {
            var settings = GetProtocolSettings(addressVersion);
            return FromExpressWallet(wallet, settings);
        }

        public static DevWallet FromExpressWallet(ExpressWallet wallet, ProtocolSettings settings)
        {
            var accounts = wallet.Accounts.Select(a => DevWalletAccount.FromExpressWalletAccount(settings, a));
            return new DevWallet(wallet.Name, settings, accounts);
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
    }
}
