using System;
using System.Linq;
using Neo.SmartContract;
using Neo.Wallets;
using Neo;

namespace NeoExpress.Models
{
    public class DevWalletAccount : WalletAccount
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

        static string ToAddress(UInt160 scriptHash, byte addressVersion = ExpressChain.AddressVersion)
        {
            byte[] data = new byte[21];
            data[0] = addressVersion;
            Buffer.BlockCopy(scriptHash.ToArray(), 0, data, 1, 20);
            return Neo.Cryptography.Helper.Base58CheckEncode(data);

        }
        public ExpressWalletAccount ToExpressWalletAccount() => new ExpressWalletAccount()
        {
            PrivateKey = key?.PrivateKey.ToHexString() ?? string.Empty,
            ScriptHash = ToAddress(ScriptHash),
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
