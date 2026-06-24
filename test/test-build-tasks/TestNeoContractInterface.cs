// Copyright (C) 2015-2026 The Neo Project.
//
// TestNeoContractInterface.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BuildTasks;
using System;
using System.IO;
using Xunit;

namespace build_tasks
{
    public class TestNeoContractInterface
    {
        [Fact]
        public void successful_operation_runs_once()
        {
            var count = 0;

            NeoContractInterface.FileOperationWithRetry(() => count++);

            Assert.Equal(1, count);
        }

        [Fact]
        public void operation_is_retried_after_a_sharing_violation()
        {
            const int ProcessCannotAccessFileHR = unchecked((int)0x80070020);
            var count = 0;

            NeoContractInterface.FileOperationWithRetry(() =>
            {
                count++;
                if (count == 1)
                    throw new IOException("file in use") { HResult = ProcessCannotAccessFileHR };
            });

            Assert.Equal(2, count);
        }

        [Fact]
        public void persistent_sharing_violation_surfaces_after_retries_are_exhausted()
        {
            const int ProcessCannotAccessFileHR = unchecked((int)0x80070020);
            var count = 0;

            void Operation()
            {
                count++;
                throw new IOException("file in use") { HResult = ProcessCannotAccessFileHR };
            }

            Assert.Throws<IOException>(() => NeoContractInterface.FileOperationWithRetry(Operation));
            Assert.Equal(6, count);
        }
    }
}
