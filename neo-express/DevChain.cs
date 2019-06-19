using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Neo.Express
{
    class DevChain
    {
        const uint MAINNET_MAGIC = 0x4F454Eu;
        const uint TESTNET_MAGIC = 0x544F454Eu;

        static uint GenerateMagicValue()
        {
            var random = new Random();

            do
            {
                uint magic = (uint)random.Next(int.MaxValue);

                // ensure the generated magic value isn't MainNet or TestNet's 
                // magic value
                if (!(magic == MAINNET_MAGIC || magic == TESTNET_MAGIC))
                {
                    return magic;
                }
            }
            while (true);
        }

        public uint Magic { get; set; }
        public List<DevWallet> Wallets { get; set; }

        public DevChain(uint magic, IEnumerable<DevWallet> wallets)
        {
            Magic = magic;
            Wallets = wallets.ToList();
        }

        public DevChain(IEnumerable<DevWallet> wallets) 
            : this(GenerateMagicValue(), wallets)
        {
        }

        public void WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WriteNumber("magic", Magic);
            writer.WriteStartArray("wallets");
            foreach (var wallet in Wallets)
            {
                wallet.WriteJson(writer);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
}
