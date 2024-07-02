// Copyright (C) 2015-2024 The Neo Project.
//
// NefFile.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace Neo.Collector.Models
{
    public partial class NefFile
    {
        public string Compiler { get; }
        public string Source { get; }
        public IReadOnlyList<MethodToken> Tokens { get; }
        public byte[] Script { get; }
        public uint CheckSum { get; }

        public NefFile(string compiler, string source, IReadOnlyList<MethodToken> tokens, byte[] script, uint checkSum)
        {
            Compiler = compiler;
            Source = source;
            Tokens = tokens;
            Script = script;
            CheckSum = checkSum;
        }

        public static bool TryLoad(string filename, [MaybeNullWhen(false)] out NefFile nefFile)
        {
            try
            {
                if (File.Exists(filename))
                {
                    nefFile = Load(filename);
                    return true;
                }
            }
            catch { }

            nefFile = default;
            return false;
        }

        public static NefFile Load(string filename)
        {
            using (var stream = File.OpenRead(filename))
            {
                return Load(stream);
            }
        }

        public static NefFile Load(Stream stream)
        {
            const int MaxScriptLength = 512 * 1024;
            const uint Magic = 0x3346454E;

            var reader = new BinaryReader(stream);
            var magic = reader.ReadUInt32();
            if (magic != Magic)
                throw new FormatException($"Invalid magic {magic}");
            var compiler = Encoding.UTF8.GetString(reader.ReadBytes(64));
            var source = reader.ReadVarString(256);
            var reserve1 = reader.ReadByte();
            if (reserve1 != 0)
                throw new FormatException($"Reserve bytes must be 0");
            var tokenCount = (int)reader.ReadVarInt(128);
            var tokens = new List<MethodToken>(tokenCount);
            for (int i = 0; i < tokenCount; i++)
            {
                tokens.Add(ReadMethodToken(reader));
            }
            var reserve2 = reader.ReadUInt16();
            if (reserve2 != 0)
                throw new FormatException($"Reserve bytes must be 0");
            var script = reader.ReadVarMemory(MaxScriptLength);
            if (script.Length == 0)
                throw new FormatException($"Script can't be empty");
            var checksum = reader.ReadUInt32();
            // TODO: Check Checksum

            return new NefFile(compiler, source, tokens, script, checksum);
        }

        static MethodToken ReadMethodToken(BinaryReader reader)
        {
            var hash = Hash160.Read(reader);
            var method = reader.ReadVarString(32);
            var parametersCount = reader.ReadUInt16();
            var hasReturnValue = reader.ReadBoolean();
            var callFlags = reader.ReadByte();

            return new MethodToken(hash, method, parametersCount, hasReturnValue, callFlags);
        }
    }
}
