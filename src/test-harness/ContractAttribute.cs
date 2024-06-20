// Copyright (C) 2015-2024 The Neo Project.
//
// ContractAttribute.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace NeoTestHarness
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
    public class ContractAttribute : Attribute
    {
        public string Name { get; private set; } = string.Empty;
        public string ManifestPath { get; private set; } = string.Empty;

        public ContractAttribute(string name, string manifestPath)
        {
            Name = name;
            ManifestPath = manifestPath;
        }
    }
}
