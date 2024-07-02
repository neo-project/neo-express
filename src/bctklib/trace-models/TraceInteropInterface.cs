// Copyright (C) 2015-2024 The Neo Project.
//
// TraceInteropInterface.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.VM.Types;

namespace Neo.BlockchainToolkit.TraceDebug
{
    public class TraceInteropInterface : StackItem
    {
        public TraceInteropInterface(string typeName)
        {
            TypeName = typeName;
        }

        public override StackItemType Type => StackItemType.InteropInterface;

        public string TypeName { get; }

        public override bool GetBoolean() => true;
    }
}
