using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Express.Backend2
{
    internal static class Extensions
    {
        public static JObject Sign(this WalletAccount account, byte[] data)
        {
            var key = account.GetKey();
            //var publicKey = key.PublicKey.EncodePoint(false).Skip(1).ToArray();
            var publicKey = key.PublicKey.EncodePoint(false).AsSpan().Slice(1).ToArray();
            var signature = Cryptography.Crypto.Default.Sign(data, key.PrivateKey, publicKey);

            return new JObject
            {
                ["signature"] = signature.ToHexString(),
                ["public-key"] = key.PublicKey.EncodePoint(true).ToHexString(),
                ["contract"] = new JObject
                {
                    ["script"] = account.Contract.Script.ToHexString(),
                    ["parameters"] = new JArray(account.Contract.ParameterList.Select(cpt => Enum.GetName(typeof(ContractParameterType), cpt)))
                }
            };
        }

        public static IEnumerable<JObject> Sign(this DevWallet wallet, IEnumerable<UInt160> hashes, byte[] data)
        {
            foreach (var hash in hashes)
            {
                var account = wallet.GetAccount(hash);
                if (account == null || !account.HasKey)
                    continue;

                yield return Sign(account, data);
            }
        }
    }
}
