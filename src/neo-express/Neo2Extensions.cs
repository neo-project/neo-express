using System;
using System.IO;
using System.Linq;
using NeoExpress.Neo2.Models;
using Newtonsoft.Json;

namespace NeoExpress
{
    static class Neo2Extensions
    {
        public static void Save(this ExpressChain chain, string fileName)
        {
            var serializer = new JsonSerializer();
            using (var stream = File.Open(fileName, FileMode.Create, FileAccess.Write))
            using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
            {
                serializer.Serialize(writer, chain);
            }
        }

                public static bool IsReservedName(this ExpressChain chain, string name)
        {
            if ("genesis".Equals(name, StringComparison.InvariantCultureIgnoreCase))
                return true;

            foreach (var node in chain.ConsensusNodes)
            {
                if (string.Equals(name, node.Wallet.Name, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }

        public static bool NameEquals(this ExpressWallet wallet, string name) =>
            string.Equals(wallet.Name, name, StringComparison.InvariantCultureIgnoreCase);

        public static ExpressWallet GetWallet(this ExpressChain chain, string name) =>
            (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => w.NameEquals(name));

        public static ExpressWalletAccount? GetAccount(this ExpressChain chain, string name)
        {
            if (chain.Wallets != null)
            {
                var wallet = chain.Wallets.SingleOrDefault(w => w.NameEquals(name));
                if (wallet != null)
                {
                    return wallet.DefaultAccount;
                }
            }

            var node = chain.ConsensusNodes.SingleOrDefault(n => n.Wallet.NameEquals(name));
            if (node != null)
            {
                return node.Wallet.DefaultAccount;
            }

            if ("genesis".Equals(name, StringComparison.InvariantCultureIgnoreCase))
            {
                return chain.ConsensusNodes
                    .Select(n => n.Wallet.Accounts.Single(a => a.Label == "MultiSigContract"))
                    .FirstOrDefault();
            }

            return null;
        }
    }
}
