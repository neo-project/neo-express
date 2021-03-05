using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json;
using Neo;
using Neo.BlockchainToolkit.Models;

namespace NeoExpress.Models
{
    class DevWallet : Wallet
    {
        private readonly string name;
        private readonly Dictionary<UInt160, DevWalletAccount> accounts = new Dictionary<UInt160, DevWalletAccount>();

        public DevWallet(string name, ProtocolSettings settings, IEnumerable<DevWalletAccount>? accounts = null) : base(string.Empty, settings)
        {
            this.name = name;

            foreach (var a in accounts ?? Enumerable.Empty<DevWalletAccount>())
            {
                this.accounts.Add(a.ScriptHash, a);
            }
        }

        public DevWallet(string name, ProtocolSettings settings, DevWalletAccount account) : base(string.Empty, settings)
        {
            this.name = name;
            accounts.Add(account.ScriptHash, account);
        }

        public ExpressWallet ToExpressWallet() => new ExpressWallet()
        {
            Name = name,
            Accounts = accounts.Values
                    .Select(a => a.ToExpressWalletAccount())
                    .ToList(),
        };

        public static DevWallet FromExpressWallet(ExpressWallet wallet)
        {
            throw new NotImplementedException();
            // var accounts = wallet.Accounts.Select(DevWalletAccount.FromExpressWalletAccount);
            // return new DevWallet(wallet.Name, accounts);
        }

        public void Export(string filename, string password)
        {
            throw new NotImplementedException();
            // var nep6Wallet = new Neo.Wallets.NEP6.NEP6Wallet(filename, Name);
            // nep6Wallet.Unlock(password);
            // foreach (var account in GetAccounts())
            // {
            //     nep6Wallet.CreateAccount(account.Contract, account.GetKey());
            // }
            // nep6Wallet.Save();
        }

        public override string Name => name;

        public override Version? Version => null;

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
            var contract = new Contract
            {
                Script = Contract.CreateSignatureRedeemScript(key.PublicKey),
                ParameterList = new[] { ContractParameterType.Signature },
            };

            var account = new DevWalletAccount(key, contract, contract.ScriptHash);
            return AddAccount(account);
        }

        public override WalletAccount CreateAccount(Contract contract, KeyPair? key = null)
        {
            var account = new DevWalletAccount(key, contract, contract.ScriptHash);
            return AddAccount(account);
        }

        public override WalletAccount CreateAccount(UInt160 scriptHash)
        {
            var account = new DevWalletAccount(null, null, scriptHash);
            return AddAccount(account);
        }

        public override bool DeleteAccount(UInt160 scriptHash) => accounts.Remove(scriptHash);

        public override WalletAccount? GetAccount(UInt160 scriptHash) => accounts.GetValueOrDefault(scriptHash);

        public override IEnumerable<WalletAccount> GetAccounts() => accounts.Values;

        public override bool VerifyPassword(string password) => true;

        public override bool ChangePassword(string oldPassword, string newPassword)
            => throw new NotImplementedException();
    }
}
