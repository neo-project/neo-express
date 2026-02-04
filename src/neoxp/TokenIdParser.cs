// Copyright (C) 2015-2026 The Neo Project.
//
// TokenIdParser.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Text;

namespace NeoExpress
{
    public static class TokenIdParser
    {
        public static ReadOnlyMemory<byte> Parse(string tokenId)
        {
            if (string.IsNullOrWhiteSpace(tokenId))
                throw new ArgumentException("tokenId cannot be null or whitespace", nameof(tokenId));

            if (tokenId.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return Convert.FromHexString(tokenId[2..]);
                }
                catch (FormatException ex)
                {
                    throw new ArgumentException($"Invalid hex token id \"{tokenId}\"", nameof(tokenId), ex);
                }
            }

            try
            {
                return Convert.FromBase64String(tokenId);
            }
            catch (FormatException)
            {
                // Allow textual token ids without forcing base64/hex formatting
                return Encoding.UTF8.GetBytes(tokenId);
            }
        }
    }
}
