using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Neo;
using NeoExpress.Models;

namespace NeoExpress
{
    static class Extensions
    {
        public static ExpressWallet? GetWallet(this ExpressChain chain, string name)
            => (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => string.Equals(name, w.Name, StringComparison.OrdinalIgnoreCase));

        public static void InitalizeProtocolSettings(this ExpressChain chain, uint secondsPerBlock = 0)
        {
            if (!chain.TryInitializeProtocolSettings(secondsPerBlock))
            {
                throw new Exception("could not initialize protocol settings");
            }
        }

        public static bool TryInitializeProtocolSettings(this ExpressChain chain, uint secondsPerBlock = 0)
        {
            secondsPerBlock = secondsPerBlock == 0 ? 15 : secondsPerBlock;

            IEnumerable<KeyValuePair<string, string>> settings()
            {
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:Magic", $"{chain.Magic}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:MillisecondsPerBlock", $"{secondsPerBlock * 1000}");
                yield return new KeyValuePair<string, string>(
                    "ProtocolConfiguration:ValidatorsCount", $"{chain.ConsensusNodes.Count}");

                foreach (var (node, index) in chain.ConsensusNodes.Select((n, i) => (n, i)))
                {
                    var privateKey = node.Wallet.Accounts
                        .Select(a => a.PrivateKey)
                        .Distinct().Single().HexToBytes();
                    var encodedPublicKey = new Neo.Wallets.KeyPair(privateKey).PublicKey
                        .EncodePoint(true).ToHexString();
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:StandbyCommittee:{index}", encodedPublicKey);
                    yield return new KeyValuePair<string, string>(
                        $"ProtocolConfiguration:SeedList:{index}", $"{System.Net.IPAddress.Loopback}:{node.TcpPort}");
                }
            }

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings())
                .Build();

            return ProtocolSettings.Initialize(config);
        }
    }
}
