// Copyright (C) 2015-2024 The Neo Project.
//
// SequencePointAttribute.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace NeoTestHarness
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
    public class SequencePointAttribute : Attribute
    {
        public string FileName { get; } = string.Empty;
        public string Namespace { get; } = string.Empty;
        public string Name { get; } = string.Empty;
        public uint Address { get; }
        public uint StartLine { get; }
        public uint StartColumn { get; }
        public uint EndLine { get; }
        public uint EndColumn { get; }

        public SequencePointAttribute(string fileName, string @namespace, string name, uint address, uint startLine, uint startColumn, uint endLine, uint endColumn)
        {
            FileName = fileName;
            Namespace = @namespace;
            Name = name;
            Address = address;
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
        }
    }
}
