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
        private readonly KeyPair? keyPair;

        public DevWalletAccount(ProtocolSettings settings, KeyPair keyPair)
            : this(settings, Contract.CreateSignatureContract(keyPair.PublicKey), keyPair)
        {
        }

        public DevWalletAccount(ProtocolSettings settings, Contract contract, KeyPair? keyPair) : base(contract.ScriptHash, settings)
        {
            this.keyPair = keyPair;
            Contract = contract;
        }

        public DevWalletAccount(ProtocolSettings settings, UInt160 scriptHash) : base(scriptHash, settings)
        {
            this.keyPair = null;
            Contract = null;
        }

        public override bool HasKey => keyPair != null;

        public override KeyPair? GetKey() => keyPair;

        public ExpressWalletAccount ToExpressWalletAccount() => new ExpressWalletAccount()
        {
            PrivateKey = keyPair?.PrivateKey.ToHexString() ?? string.Empty,
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
