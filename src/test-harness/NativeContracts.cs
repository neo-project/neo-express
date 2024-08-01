// Copyright (C) 2015-2024 The Neo Project.
//
// NativeContracts.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;

namespace NeoTestHarness
{
    public static class NativeContracts
    {
        static Lazy<UInt160> neoToken = new Lazy<UInt160>(() => UInt160.Parse("0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5"));
        public static UInt160 NeoToken => neoToken.Value;

        static Lazy<UInt160> gasToken = new Lazy<UInt160>(() => UInt160.Parse("0xd2a4cff31913016155e38e474a2c06d08be276cf"));
        public static UInt160 GasToken => gasToken.Value;
    }
}
