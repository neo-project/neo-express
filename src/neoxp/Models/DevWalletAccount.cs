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

        public DevWalletAccount(ProtocolSettings settings, KeyPair keyPair)
            : this(settings, keyPair, Contract.CreateSignatureContract(keyPair.PublicKey))
        {
        }

        public DevWalletAccount(ProtocolSettings settings, KeyPair? key, Contract contract) : base(contract.ScriptHash, settings)
        {
            this.key = key;
            Contract = contract;
        }

        public DevWalletAccount(ProtocolSettings settings, UInt160 scriptHash) : base(scriptHash, settings)
        {
            this.key = null;
            Contract = null;
        }

        public override bool HasKey => key != null;

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
            return new DevWalletAccount(settings, keyPair)
            {
                Label = account.Label,
                IsDefault = account.IsDefault
            };
        }
    }
}
