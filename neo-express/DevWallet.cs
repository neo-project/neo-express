using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Neo.SmartContract;
using Neo.Wallets;

namespace Neo.Express
{
    class DevWallet : Wallet
    {
        private readonly string name;
        private readonly Dictionary<UInt160, DevWalletAccount> accounts = new Dictionary<UInt160, DevWalletAccount>();

        public DevWallet(string name, IEnumerable<DevWalletAccount> accounts = null)
        {
            this.name = name;

            foreach (var a in accounts ?? System.Linq.Enumerable.Empty<DevWalletAccount>())
            {
                this.accounts.Add(a.ScriptHash, a);
            }
        }

        public void WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteString("name", Name);
            writer.WriteStartArray("accounts");
            foreach (var a in accounts.Values)
            {
                a.WriteJson(writer);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        public override string Name => name;

        public override Version Version => null;

        public override bool Contains(UInt160 scriptHash) => accounts.ContainsKey(scriptHash);

        public override WalletAccount CreateAccount(byte[] privateKey)
        {
            var key = new KeyPair(privateKey);
            var contract = new Contract
            {
                Script = Contract.CreateSignatureRedeemScript(key.PublicKey),
                ParameterList = new[] { ContractParameterType.Signature },
            };

            var account = new DevWalletAccount(key, contract, contract.ScriptHash);
            lock (accounts)
            {
                accounts.Add(account.ScriptHash, account);
            }
            return account;
        }

        public override WalletAccount CreateAccount(Contract contract, KeyPair key = null)
        {
            var account = new DevWalletAccount(key, contract, contract.ScriptHash);
            lock (accounts)
            {
                accounts.Add(account.ScriptHash, account);
            }
            return account;
        }

        public override WalletAccount CreateAccount(UInt160 scriptHash)
        {
            throw new NotImplementedException();
        }

        public override bool DeleteAccount(UInt160 scriptHash) => accounts.Remove(scriptHash);

        public override WalletAccount GetAccount(UInt160 scriptHash) => accounts.GetValueOrDefault(scriptHash);

        public override IEnumerable<WalletAccount> GetAccounts() => accounts.Values;

        public override bool VerifyPassword(string password) => true;
    }
}
