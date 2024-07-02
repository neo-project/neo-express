// Copyright (C) 2015-2024 The Neo Project.
//
// RocksDbUtility.PinnableSlice.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using RocksDbSharp;

namespace Neo.BlockchainToolkit.Persistence
{
    public static partial class RocksDbUtility
    {
        public struct PinnableSlice : IDisposable
        {
            public IntPtr Handle { get; internal set; }

            public PinnableSlice(IntPtr handle)
            {
                Handle = handle;
            }

            public bool Valid => Handle != IntPtr.Zero;

            public unsafe ReadOnlySpan<byte> GetValue()
            {
                if (Handle == IntPtr.Zero)
                    return default;
                var valuePtr = Native.Instance.rocksdb_pinnableslice_value(Handle, out var valueLength);
                if (valuePtr == IntPtr.Zero)
                    return default;
                return new ReadOnlySpan<byte>((byte*)valuePtr, (int)valueLength);
            }

            public void Dispose()
            {
                if (Handle != IntPtr.Zero)
                {
                    var handle = Handle;
                    Handle = IntPtr.Zero;
                    Native.Instance.rocksdb_pinnableslice_destroy(handle);
                }
            }
        }
    }
}
