// Copyright (C) 2015-2026 The Neo Project.
//
// DebugView.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace NeoDebug.Neo3
{
    /// <summary>How a debug session presents the current location: as C# source or as NeoVM disassembly.</summary>
    public enum DebugView
    {
        Source,
        Disassembly,
        Toggle,
    }
}
