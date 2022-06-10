using System.Linq;
using Neo;
using Neo.BlockchainToolkit.Models;
using NeoExpress.Node;

namespace NeoExpress
{
    static class TestableExtensions
    {
        public static bool IsRunning(this IExpressChain @this)
        {
            for (var i = 0; i < @this.ConsensusNodes.Count; i++)
            {
                if (@this.ConsensusNodes[i].IsRunning())
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsRunning(this ExpressConsensusNode node)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            var account = node.Wallet.Accounts.Single(a => a.IsDefault);
            return System.Threading.Mutex.TryOpenExisting(
                ExpressSystem.GLOBAL_PREFIX + account.ScriptHash,
                out var _);
        }

        public static ProtocolSettings GetProtocolSettings(this IExpressChain? chain, uint? secondsPerBlock = null)
        {

            var blockTime = secondsPerBlock.HasValue
                ? secondsPerBlock.Value
                : (chain?.TryReadSetting("chain.SecondsPerBlock", uint.TryParse, out uint setting) ?? false)
                    ? setting
                    : 0;

            return chain == null
                ? ProtocolSettings.Default
                : ProtocolSettings.Default with
                {
                    Network = chain.Network,
                    AddressVersion = chain.AddressVersion,
                    MillisecondsPerBlock = blockTime == 0 ? 15000 : blockTime * 1000,
                    ValidatorsCount = chain.ConsensusNodes.Count,
                    StandbyCommittee = chain.ConsensusNodes.Select(GetPublicKey).ToArray(),
                    SeedList = chain.ConsensusNodes
                        .Select(n => $"{System.Net.IPAddress.Loopback}:{n.TcpPort}")
                        .ToArray(),
                };

            static Neo.Cryptography.ECC.ECPoint GetPublicKey(ExpressConsensusNode node)
                => new Neo.Wallets.KeyPair(node.Wallet.Accounts.Select(a => a.PrivateKey).Distinct().Single().HexToBytes()).PublicKey;
        }
    }
}
