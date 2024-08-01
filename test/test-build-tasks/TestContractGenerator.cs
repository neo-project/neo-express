// Copyright (C) 2015-2024 The Neo Project.
//
// TestContractGenerator.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BuildTasks;
using System;
using System.IO;
using Xunit;

namespace build_tasks
{
    public class TestContractGenerator
    {
        [Fact]
        public void throws_on_invalid_type_name()
        {
            var manifest = new NeoManifest() { Name = "Invalid Type Name" };
            Assert.ThrowsAny<Exception>(() => ContractGenerator.GenerateContractInterface(manifest, "", "", ""));
        }

        [Fact]
        public void generates_on_valid_type_name()
        {
            var manifest = new NeoManifest() { Name = "ValidTypeName" };
            var manifestPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var code = ContractGenerator.GenerateContractInterface(manifest, manifestPath, "", "");
            Assert.NotEqual(-1, code.IndexOf("interface ValidTypeName"));
        }

        [Fact]
        public void generates_on_dotted_type_name()
        {
            var manifest = new NeoManifest() { Name = "Dotted.Type.Name" };
            var manifestPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var code = ContractGenerator.GenerateContractInterface(manifest, manifestPath, "", "");
            Assert.NotEqual(-1, code.IndexOf("interface Name"));
        }

        [Fact]
        public void generates_on_contract_name_override()
        {
            var manifest = new NeoManifest() { Name = "Invalid Type Name" };
            var manifestPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var code = ContractGenerator.GenerateContractInterface(manifest, manifestPath, "ValidTypeName", "");
            Assert.NotEqual(-1, code.IndexOf("interface ValidTypeName"));
        }

        [Fact]
        public void throws_on_invalid_contract_name_override()
        {
            var manifest = new NeoManifest() { Name = "ValidTypeName" };
            Assert.ThrowsAny<Exception>(() => ContractGenerator.GenerateContractInterface(manifest, "", "Invalid Type Name", ""));
        }

        [Fact]
        public void throws_on_dotted_contract_name_override()
        {
            var manifest = new NeoManifest() { Name = "ValidTypeName" };
            Assert.ThrowsAny<Exception>(() => ContractGenerator.GenerateContractInterface(manifest, "", "Dotted.Type.Name", ""));
        }
    }
}
