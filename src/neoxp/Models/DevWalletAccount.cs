using System;
using System.Linq;
using Neo.SmartContract;
using Neo.Wallets;
using Neo;
using Neo.BlockchainToolkit.Models;

namespace NeoExpress.Models
{
    class DevWalletAccount : WalletAccount
    {
        private readonly KeyPair? key;

        public DevWalletAccount(KeyPair? key, Contract? contract, UInt160 scriptHash) : base(scriptHash)
        {
            this.key = key;
            Contract = contract;
        }

        public override bool HasKey => key != null;

        public override KeyPair? GetKey()
        {
            return key;
        }

        public ExpressWalletAccount ToExpressWalletAccount() => new ExpressWalletAccount()
        {
            PrivateKey = key?.PrivateKey.ToHexString() ?? string.Empty,
            ScriptHash = ScriptHash.ToAddress(),
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

        public static DevWalletAccount FromExpressWalletAccount(ExpressWalletAccount account)
        {
            var keyPair = new KeyPair(account.PrivateKey.HexToBytes());
            var contract = new Contract()
            {
                Script = account.Contract?.Script.HexToBytes(),
                ParameterList = account.Contract?.Parameters
                    .Select(Enum.Parse<ContractParameterType>)
                    .ToArray()
            };
            var scriptHash = account.ScriptHash.ToScriptHash();

            return new DevWalletAccount(keyPair, contract, scriptHash)
            {
                Label = account.Label,
                IsDefault = account.IsDefault
            };
        }
    }
}
