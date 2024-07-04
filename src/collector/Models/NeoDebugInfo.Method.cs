// Copyright (C) 2015-2024 The Neo Project.
//
// NeoDebugInfo.Method.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;

namespace Neo.Collector.Models
{
    public partial class NeoDebugInfo
    {
        public struct Method
        {
            public readonly string Id;
            public readonly string Namespace;
            public readonly string Name;
            public readonly (int Start, int End) Range;
            public readonly IReadOnlyList<Parameter> Parameters;
            public readonly IReadOnlyList<SequencePoint> SequencePoints;

            public Method(string id, string @namespace, string name, (int, int) range, IReadOnlyList<Parameter> parameters, IReadOnlyList<SequencePoint> sequencePoints)
            {
                Id = id;
                Namespace = @namespace;
                Name = name;
                Range = range;
                Parameters = parameters;
                SequencePoints = sequencePoints;
            }
        }
    }
}
