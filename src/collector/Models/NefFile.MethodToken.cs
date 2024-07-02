// Copyright (C) 2015-2024 The Neo Project.
//
// NefFile.MethodToken.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Collector.Models
{
    public partial class NefFile
    {
        public class MethodToken
        {
            public Hash160 Hash { get; }
            public string Method { get; }
            public ushort ParametersCount { get; }
            public bool HasReturnValue { get; }
            public byte CallFlags { get; }

            public MethodToken(Hash160 hash, string method, ushort parametersCount, bool hasReturnValue, byte callFlags)
            {
                Hash = hash;
                Method = method;
                ParametersCount = parametersCount;
                HasReturnValue = hasReturnValue;
                CallFlags = callFlags;
            }
        }
    }
}
