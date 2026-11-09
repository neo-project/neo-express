// Copyright (C) 2015-2026 The Neo Project.
//
// DebugSession.Prefixes.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace NeoDebug.Neo3
{
    // The expression prefixes that name the VM's slots and storage in scopes and evaluate expressions
    // (for example "#local0" or "#storage[..]"). Defined here so the variable containers can reference them
    // before the rest of DebugSession exists; the session behaviour is added in the same partial class later.
    internal partial class DebugSession
    {
        public const string EVAL_STACK_PREFIX = "#eval";
        public const string RESULT_STACK_PREFIX = "#result";
        public const string STORAGE_PREFIX = "#storage";
        public const string ARG_SLOTS_PREFIX = "#arg";
        public const string LOCAL_SLOTS_PREFIX = "#local";
        public const string STATIC_SLOTS_PREFIX = "#static";
    }
}
