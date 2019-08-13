using Microsoft.Extensions.Configuration;
using Neo.Express.Abstractions;
using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

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

        public static bool InitializeProtocolSettings(this ExpressChain chain, uint secondsPerBlock = 0)
        {
            secondsPerBlock = secondsPerBlock == 0 ? 15 : secondsPerBlock;

            IEnumerable<KeyValuePair<string, string>> settings()
            {
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:Magic", $"{chain.Magic}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:AddressVersion", $"{(byte)0x17}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:SecondsPerBlock", $"{secondsPerBlock}");

                foreach (var (node, index) in chain.ConsensusNodes.Select((n, i) => (n, i)))
                {
                    var privateKey = node.Wallet.Accounts
                        .Select(a => a.PrivateKey)
                        .Distinct().Single().HexToBytes();
                    var encodedPublicKey = new KeyPair(privateKey).PublicKey
                        .EncodePoint(true).ToHexString();
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:StandbyValidators:{index}", encodedPublicKey);
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:SeedList:{index}", $"{IPAddress.Loopback}:{node.TcpPort}");
                }
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings())
                .Build();

            return ProtocolSettings.Initialize(config);
        }


    }
}
