// Copyright (C) 2015-2026 The Neo Project.
//
// CastOperation.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace NeoDebug.Neo3
{
    /// <summary>How an evaluate expression or a method return value should be rendered (e.g. <c>(int)</c>, <c>(addr)</c>).</summary>
    internal enum CastOperation
    {
        None,
        Integer,
        Boolean,
        String,
        HexString,
        ByteArray,
        Address,
    }
}
