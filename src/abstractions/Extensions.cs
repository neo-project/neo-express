using System;
using System.IO;
using System.Linq;
using System.Text;
using NeoExpress.Abstractions.Models;
using Newtonsoft.Json;

namespace NeoExpress.Abstractions
{
    public static class Extensions
    {
        public static string ToHexString(this byte[] value, bool reverse = false)
        {
            var sb = new StringBuilder();

            if (reverse)
            {
                for (int i = value.Length - 1; i >= 0; i--)
                {
                    sb.AppendFormat("{0:x2}", value[i]);
                }
            }
            else
            {
                for (int i = 0; i < value.Length; i++)
                {
                    sb.AppendFormat("{0:x2}", value[i]);
                }
            }
            return sb.ToString();
        }

        public static byte[] ToByteArray(this string value)
        {
            if (value == null || value.Length == 0)
                return new byte[0];
            if (value.Length % 2 == 1)
                throw new FormatException();
            byte[] result = new byte[value.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = byte.Parse(value.Substring(i * 2, 2), System.Globalization.NumberStyles.AllowHexSpecifier);
            return result;
        }

        public static ExpressWallet? GetWallet(this ExpressChain chain, string name)
            => (chain.Wallets ?? Enumerable.Empty<ExpressWallet>())
                .SingleOrDefault(w => string.Equals(name, w.Name, StringComparison.OrdinalIgnoreCase));
    }
}
