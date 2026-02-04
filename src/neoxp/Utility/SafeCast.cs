// Copyright (C) 2015-2025 The Neo Project.
//
// SafeCast.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Numerics;

namespace NeoExpress.Utility;

/// <summary>
/// Provides safe casting methods for BigInteger to common integer types,
/// handling the Neo VM's signed/unsigned encoding quirks.
/// </summary>
internal static class SafeCast
{
    /// <summary>
    /// Safely casts a BigInteger to int, handling values that may be
    /// encoded as negative due to VM representation.
    /// </summary>
    public static int ToInt32(BigInteger value)
    {
        if (value < int.MinValue || value > int.MaxValue)
            throw new OverflowException($"Value {value} is outside the range of Int32");
        return (int)value;
    }

    /// <summary>
    /// Safely casts a BigInteger to uint, handling values that may be
    /// encoded as negative due to VM representation.
    /// </summary>
    public static uint ToUInt32(BigInteger value)
    {
        // Handle values that might be returned as negative due to VM encoding
        if (value < 0)
        {
            if (value >= int.MinValue)
                return unchecked((uint)(int)value);
            throw new OverflowException($"Value {value} is too small for UInt32");
        }

        if (value > uint.MaxValue)
            throw new OverflowException($"Value {value} is too large for UInt32");

        return (uint)value;
    }

    /// <summary>
    /// Safely casts a BigInteger to byte, with range checking.
    /// </summary>
    public static byte ToByte(BigInteger value)
    {
        if (value < byte.MinValue || value > byte.MaxValue)
            throw new OverflowException($"Value {value} is outside the range of Byte");
        return (byte)value;
    }

    /// <summary>
    /// Safely casts a double (from JSON) to int, with range checking.
    /// </summary>
    public static int ToInt32(double value)
    {
        if (value < int.MinValue || value > int.MaxValue)
            throw new OverflowException($"Value {value} is outside the range of Int32");
        return (int)value;
    }

    /// <summary>
    /// Safely casts a double (from JSON) to uint, with range checking.
    /// </summary>
    public static uint ToUInt32(double value)
    {
        if (value < uint.MinValue || value > uint.MaxValue)
            throw new OverflowException($"Value {value} is outside the range of UInt32");
        return (uint)value;
    }
}
