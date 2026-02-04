// Copyright (C) 2015-2026 The Neo Project.
//
// CheckpointPathAttribute.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace NeoTestHarness
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CheckpointPathAttribute : Attribute
    {
        public string Path { get; private set; } = string.Empty;

        public CheckpointPathAttribute(string path)
        {
            Path = path;
        }
    }
}

