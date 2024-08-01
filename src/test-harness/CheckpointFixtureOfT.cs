// Copyright (C) 2015-2024 The Neo Project.
//
// CheckpointFixtureOfT.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace NeoTestHarness
{
    public class CheckpointFixture<T> : CheckpointFixture
    {
        static string GetCheckpointPath()
        {
            var attrib = Attribute.GetCustomAttribute(typeof(T), typeof(CheckpointPathAttribute)) as CheckpointPathAttribute;
            return attrib?.Path ?? throw new Exception($"Missing {nameof(CheckpointPathAttribute)} on {typeof(T).Name}");
        }

        public CheckpointFixture() : base(GetCheckpointPath())
        {
        }
    }
}

