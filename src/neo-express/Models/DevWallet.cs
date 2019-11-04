using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json;
using Neo;
using NeoExpress.Abstractions.Models;

namespace NeoExpress.Models
{
    public class DevWallet : Wallet
    {
        private readonly string name;
        private readonly Dictionary<UInt160, DevWalletAccount> accounts = new Dictionary<UInt160, DevWalletAccount>();

        public DevWallet(string name, IEnumerable<DevWalletAccount>? accounts = null)
        {
            this.name = name;

            foreach (var a in accounts ?? Enumerable.Empty<DevWalletAccount>())
            {
                this.accounts.Add(a.ScriptHash, a);
            }
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
            var accounts = wallet.Accounts.Select(DevWalletAccount.FromExpressWalletAccount);
            return new DevWallet(wallet.Name, accounts);
        }

        public void Export(string filename, string password)
        {
            var nep6Wallet = new Neo.Wallets.NEP6.NEP6Wallet(null, filename, Name);
            nep6Wallet.Unlock(password);
            foreach (var account in GetAccounts())
            {
                nep6Wallet.CreateAccount(account.Contract, account.GetKey());
            }
            nep6Wallet.Save();
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

        [JsonIgnore]
        public override uint WalletHeight => throw new NotImplementedException();

        public override event EventHandler<WalletTransactionEventArgs> WalletTransaction
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }

        public override void ApplyTransaction(Transaction tx)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<Coin> GetCoins(IEnumerable<UInt160> accounts)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<UInt256> GetTransactions()
        {
            throw new NotImplementedException();
        }
    }
}
