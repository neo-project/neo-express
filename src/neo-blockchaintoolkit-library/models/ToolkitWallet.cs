// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Neo.BlockchainToolkit.Models
{
    public partial class ToolkitWallet : Wallet
    {
        readonly Dictionary<UInt160, Account> accounts = new();

        public override string Name { get; }
        public override Version Version => Version.Parse(Assembly.GetExecutingAssembly().ImageRuntimeVersion);

        public ToolkitWallet(string name, ProtocolSettings settings) : base(string.Empty, settings)
        {
            this.Name = name;
        }

        internal ToolkitWallet(string name, IEnumerable<Account> accounts, ProtocolSettings settings)
            : this(name, settings)
        {
            foreach (var account in accounts)
            {
                this.accounts.Add(account.ScriptHash, account);
            }
        }

        public static ToolkitWallet Parse(JObject json, ProtocolSettings settings)
        {
            var name = json.Value<string>("name") ?? "";
            var wallet = new ToolkitWallet(name, settings);

            IEnumerable<JToken> accountsJson = json["accounts"] as JArray ?? Enumerable.Empty<JToken>();
            foreach (var accountToken in accountsJson.Cast<JObject>())
            {
                var account = Account.Parse(accountToken, settings);
                wallet.accounts.Add(account.ScriptHash, account);
            }
            return wallet;
        }

        public void WriteJson(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteProperty("name", Name);
            writer.WritePropertyName("accounts");
            writer.WriteStartArray();
            foreach (var account in accounts.Values)
            {
                account.WriteJson(writer);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        public override bool Contains(UInt160 scriptHash) => accounts.ContainsKey(scriptHash);

        public override WalletAccount CreateAccount(byte[] privateKey)
        {
            var key = new KeyPair(privateKey);
            var contract = Contract.CreateSignatureContract(key.PublicKey);
            return CreateAccount(contract, key);
        }

        public override WalletAccount CreateAccount(Contract contract, KeyPair key = null)
        {
            var account = new Account(key, contract.ScriptHash, ProtocolSettings)
            {
                Contract = contract
            };
            accounts.Add(account.ScriptHash, account);
            return account;
        }

        public override WalletAccount CreateAccount(UInt160 scriptHash)
        {
            var account = new Account(null, scriptHash, ProtocolSettings);
            accounts.Add(account.ScriptHash, account);
            return account;
        }

        public override bool DeleteAccount(UInt160 scriptHash) => accounts.Remove(scriptHash);

        public override WalletAccount GetAccount(UInt160 scriptHash) => accounts.GetValueOrDefault(scriptHash);

        public override IEnumerable<WalletAccount> GetAccounts() => accounts.Values;

        public override bool VerifyPassword(string password)
            => throw new NotSupportedException();

        public override bool ChangePassword(string oldPassword, string newPassword)
            => throw new NotSupportedException();

        public override void Delete()
            => throw new NotSupportedException();

        public override void Save()
            => throw new NotSupportedException();
    }
}
