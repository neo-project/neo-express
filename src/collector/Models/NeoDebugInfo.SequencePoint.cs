// Copyright (C) 2015-2024 The Neo Project.
//
// NeoDebugInfo.SequencePoint.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Collector.Models
{
    public partial class NeoDebugInfo
    {
        public struct SequencePoint
        {
            public readonly int Address;
            public readonly int Document;
            public readonly (int Line, int Column) Start;
            public readonly (int Line, int Column) End;

            public SequencePoint(int address, int document, (int, int) start, (int, int) end)
            {
                Address = address;
                Document = document;
                Start = start;
                End = end;
            }
        }
    }
}
