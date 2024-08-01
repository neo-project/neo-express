// Copyright (C) 2015-2024 The Neo Project.
//
// Utility.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.MPTTrie;
using Neo.IO;
using Neo.SmartContract;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using static Neo.BlockchainToolkit.Constants;

namespace Neo.BlockchainToolkit
{
    public static class Utility
    {

        public static bool TryParseRpcUri(string value, [NotNullWhen(true)] out Uri? uri)
        {
            if (value.Equals("mainnet", StringComparison.OrdinalIgnoreCase))
            {
                uri = new Uri(MAINNET_RPC_ENDPOINTS[0]);
                return true;
            }

            if (value.Equals("testnet", StringComparison.OrdinalIgnoreCase))
            {
                uri = new Uri(TESTNET_RPC_ENDPOINTS[0]);
                return true;
            }

            return Uri.TryCreate(value, UriKind.Absolute, out uri)
                && uri is not null
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public static (StorageKey key, byte[] value) VerifyProof(UInt256 rootHash, byte[]? proof)
        {
            ArgumentNullException.ThrowIfNull(proof);

            var proofs = new HashSet<byte[]>();

            using MemoryStream stream = new(proof, false);
            using BinaryReader reader = new(stream, Neo.Utility.StrictUTF8);

            var keyBuffer = reader.ReadVarBytes(Node.MaxKeyLength);

            var count = reader.ReadVarInt();
            for (ulong i = 0; i < count; i++)
            {
                proofs.Add(reader.ReadVarBytes());
            }

            var value = Trie.VerifyProof(rootHash, keyBuffer, proofs);
            if (value is null)
                throw new Exception("Verification failed");

            // Note, StorageKey.Deserialized was removed in Neo 3.3.0
            //       so VerifyProof has to deserialize StorageKey directly
            //       https://github.com/neo-project/neo/issues/2765
            var key = new StorageKey()
            {
                Id = BinaryPrimitives.ReadInt32LittleEndian(keyBuffer),
                Key = keyBuffer.AsMemory(4)
            };
            return (key, value);
        }
    }
}
