// Copyright (C) 2015-2026 The Neo Project.
//
// TestContractGeneratorEscaping.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BuildTasks;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace build_tasks
{
    public class TestContractGeneratorEscaping
    {
        [Fact]
        public void escapes_method_name_that_is_a_keyword()
        {
            var manifest = new NeoManifest()
            {
                Name = "C",
                Methods = new List<NeoManifest.Method>
                {
                    new NeoManifest.Method { Name = "lock", ReturnType = "Void" },
                },
            };
            var manifestPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var code = ContractGenerator.GenerateContractInterface(manifest, manifestPath, "", "");
            Assert.Contains("void @lock(", code);
            Assert.DoesNotContain("void lock(", code);
        }

        [Fact]
        public void escapes_event_name_that_is_a_keyword()
        {
            var manifest = new NeoManifest()
            {
                Name = "C",
                Events = new List<NeoManifest.Event>
                {
                    new NeoManifest.Event { Name = "event" },
                },
            };
            var manifestPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var code = ContractGenerator.GenerateContractInterface(manifest, manifestPath, "", "");
            Assert.Contains("void @event(", code);
            Assert.DoesNotContain("void event(", code);
        }
    }
}
