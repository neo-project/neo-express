// Copyright (C) 2015-2025 The Neo Project.
//
// NeoAssertionsExtensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using Neo.VM.Types;

namespace Neo.Assertions
{
    public static class NeoAssertionsExtensions
    {
        public static StackItemAssertions Should(this StackItem item) => new StackItemAssertions(item);

        public static NotifyEventArgsAssertions Should(this NotifyEventArgs args) => new NotifyEventArgsAssertions(args);

        public static StorageItemAssertions Should(this StorageItem item) => new StorageItemAssertions(item);
    }
}
