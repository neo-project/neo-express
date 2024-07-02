// Copyright (C) 2015-2024 The Neo Project.
//
// NeoCsc.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.BuildTasks
{
    public class NeoCsc : DotNetToolTask
    {
        readonly static NugetPackageVersion REQUIRED_VERSION = new NugetPackageVersion(3, 3, 0);
        const string PACKAGE_ID = "Neo.Compiler.CSharp";
        const string COMMAND = "nccs";
        const byte DEFAULT_ADDRESS_VERSION = 53;

        ITaskItem[] outputFiles = Array.Empty<ITaskItem>();

        protected override string Command => COMMAND;
        protected override string PackageId => PACKAGE_ID;

        public ITaskItem[] Sources { get; set; } = Array.Empty<ITaskItem>();
        public ITaskItem? Output { get; set; }
        public string BaseFileName { get; set; } = "";
        public bool Debug { get; set; }
        public bool Assembly { get; set; }
        public bool Optimize { get; set; }
        public bool Inline { get; set; }
        public byte AddressVersion { get; set; } = DEFAULT_ADDRESS_VERSION;

        [Output]
        public ITaskItem[] OutputFiles => outputFiles;

        protected override bool ValidateVersion(NugetPackageVersion version)
        {
            if (version < REQUIRED_VERSION)
            {
                Log.LogWarning($"{nameof(NeoCsc)} requires {REQUIRED_VERSION}. {version} found");
                return false;
            }
            return true;
        }

        protected override string GetArguments()
        {
            var builder = new StringBuilder();
            foreach (var file in Sources)
            {
                builder.AppendFormat(" {0}", file.ItemSpec);
            }

            if (!(Output is null))
            {
                builder.AppendFormat(" --output {0}", Output.ItemSpec);
            }

            if (!string.IsNullOrEmpty(BaseFileName))
            {
                builder.AppendFormat(" --base-name {0}", BaseFileName);
            }

            if (Debug)
                builder.Append(" --debug");
            if (Assembly)
                builder.Append(" --assembly");
            if (!Optimize)
                builder.Append(" --no-optimize");
            if (!Inline)
                builder.Append(" --no-inline");
            if (AddressVersion != DEFAULT_ADDRESS_VERSION)
            {
                builder.AppendFormat(" --address-version {0}", AddressVersion);
            }

            return builder.ToString();
        }

        protected override void ExecutionSuccess(IReadOnlyCollection<string> output)
        {
            const string CREATED = "Created ";

            outputFiles = output
                .Where(o => o.StartsWith(CREATED))
                .Select(o => new TaskItem(o.Substring(CREATED.Length)))
                .ToArray();

            base.ExecutionSuccess(output);
        }
    }
}
