// Copyright (C) 2015-2026 The Neo Project.
//
// NefFileTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Collector.Models;
using System;
using System.IO;
using Xunit;

namespace test_collector;

public class NefFileTests
{
    const string NefResourceName = "0xe8c2cf8b50016c94f6eafbdd024febc6cd0672fe.nef";

    static byte[] GetNefBytes()
    {
        using var stream = TestFiles.GetResourceStream(NefResourceName);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Load_accepts_a_real_nef_with_a_valid_checksum()
    {
        // A real compiled contract round-trips, which also confirms the checksum
        // is computed the same way the compiler writes it.
        var nef = NefFile.Load(new MemoryStream(GetNefBytes()));

        Assert.NotEmpty(nef.Script);
    }

    [Fact]
    public void Load_rejects_a_nef_whose_checksum_does_not_match()
    {
        var bytes = GetNefBytes();
        bytes[^1] ^= 0xFF; // corrupt the trailing checksum

        Assert.Throws<FormatException>(() => NefFile.Load(new MemoryStream(bytes)));
    }

    [Fact]
    public void Load_rejects_a_nef_whose_checksum_does_not_match_from_a_non_seekable_stream()
    {
        var bytes = GetNefBytes();
        bytes[^1] ^= 0xFF; // corrupt the trailing checksum

        using var stream = new NonSeekableStream(bytes);

        Assert.Throws<FormatException>(() => NefFile.Load(stream));
    }

    sealed class NonSeekableStream : Stream
    {
        readonly MemoryStream inner;

        public NonSeekableStream(byte[] buffer)
        {
            inner = new MemoryStream(buffer);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
