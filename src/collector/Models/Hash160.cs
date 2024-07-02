// Copyright (C) 2015-2024 The Neo Project.
//
// Hash160.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.IO;
using System.Text;

namespace Neo.Collector.Models
{
    public struct Hash160 : IComparable<Hash160>, IEquatable<Hash160>
    {
        public static readonly Hash160 Zero = new Hash160(0, 0, 0);
        public const int Size = 2 * sizeof(ulong) + sizeof(uint);

        readonly ulong data1;
        readonly ulong data2;
        readonly uint data3;

        internal Hash160(ulong data1, ulong data2, uint data3)
        {
            this.data1 = data1;
            this.data2 = data2;
            this.data3 = data3;
        }

        public static Hash160 Read(BinaryReader reader)
        {
            var value1 = reader.ReadUInt64();
            var value2 = reader.ReadUInt64();
            var value3 = reader.ReadUInt32();
            return new Hash160(value1, value2, value3);
        }

        public static bool TryParse(string @string, out Hash160 result)
        {
            if (@string.TryParseHexString(out var buffer)
                && buffer.Length == Size)
            {
                Array.Reverse(buffer);
                using (var stream = new MemoryStream(buffer))
                using (var reader = new BinaryReader(stream))
                {
                    result = Read(reader);
                    return true;
                }
            }

            result = default;
            return false;
        }

        public static Hash160 Parse(string @string)
        {
            if (TryParse(@string, out var value))
                return value;
            throw new InvalidOperationException($"Failed to parse {@string}");
        }

        public int CompareTo(in Hash160 other)
        {
            var result = data1.CompareTo(other.data1);
            if (result != 0)
                return result;

            result = data2.CompareTo(other.data2);
            if (result != 0)
                return result;

            return data3.CompareTo(other.data3);
        }

        int IComparable<Hash160>.CompareTo(Hash160 other) => CompareTo(other);

        public bool Equals(in Hash160 other)
            => data1 == other.data1
                && data2 == other.data2
                && data3 == other.data3;


        bool IEquatable<Hash160>.Equals(Hash160 other) => Equals(other);

        public override bool Equals(object obj) => obj is Hash160 value && Equals(value);

        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                // Suitable nullity checks etc, of course :)
                hash = hash * 23 + data1.GetHashCode();
                hash = hash * 23 + data2.GetHashCode();
                hash = hash * 23 + data3.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            var buffer = new byte[Size];
            BitConverter.GetBytes(data1).CopyTo(buffer, 0);
            BitConverter.GetBytes(data2).CopyTo(buffer, sizeof(ulong));
            BitConverter.GetBytes(data3).CopyTo(buffer, 2 * sizeof(ulong));

            var builder = new StringBuilder("0x", 2 + Size * 2);
            for (int i = 0; i < buffer.Length; i++)
                builder.AppendFormat("{0:x2}", buffer[buffer.Length - i - 1]);
            return builder.ToString();
        }

        public static bool operator ==(in Hash160 left, in Hash160 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in Hash160 left, in Hash160 right)
        {
            return !left.Equals(right);
        }
    }
}
