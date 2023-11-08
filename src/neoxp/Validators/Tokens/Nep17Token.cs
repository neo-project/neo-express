// Copyright (C) 2015-2023 The Neo Project.
//
// The neo is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.Persistence;

namespace NeoExpress.Validators.Tokens;

internal class Nep17Token : TokenBase
{
    public Nep17Token(
        ProtocolSettings protocolSettings,
        DataCache snapshot,
        UInt160 scriptHash) : base(protocolSettings, snapshot, scriptHash)
    {
    }
}
