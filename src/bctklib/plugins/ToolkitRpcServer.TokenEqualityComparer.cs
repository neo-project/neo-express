// Copyright (C) 2015-2024 The Neo Project.
//
// ToolkitRpcServer.TokenEqualityComparer.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.BlockchainToolkit.Plugins
{
    public static partial class ToolkitRpcServer
    {
        class TokenEqualityComparer : IEqualityComparer<(UInt160 scriptHash, ReadOnlyMemory<byte> tokenId)>
        {
            public static TokenEqualityComparer Instance = new();

            private TokenEqualityComparer() { }

            public bool Equals((UInt160 scriptHash, ReadOnlyMemory<byte> tokenId) x, (UInt160 scriptHash, ReadOnlyMemory<byte> tokenId) y)
                => x.scriptHash.Equals(y.scriptHash)
                    && x.tokenId.Span.SequenceEqual(y.tokenId.Span);

            public int GetHashCode((UInt160 scriptHash, ReadOnlyMemory<byte> tokenId) obj)
            {
                HashCode code = new();
                code.Add(obj.scriptHash);
                code.AddBytes(obj.tokenId.Span);
                return code.ToHashCode();
            }
        }
    }
}
