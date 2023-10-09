// Copyright (C) 2023 neo-project
//
//  neo-express is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

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
