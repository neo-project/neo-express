// Copyright (C) 2015-2024 The Neo Project.
//
// NeoExpressBatch.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Build.Framework;
using System;
using System.Text;

namespace Neo.BuildTasks
{
    public class NeoExpressBatch : DotNetToolTask
    {
        const string PACKAGE_ID = "Neo.Express";
        const string COMMAND = "neoxp";

        protected override string Command => COMMAND;
        protected override string PackageId => PACKAGE_ID;

        [Required]
        public ITaskItem? BatchFile { get; set; }

        public ITaskItem? InputFile { get; set; }

        public bool Reset { get; set; }

        public ITaskItem? Checkpoint { get; set; }

        public bool Trace { get; set; }

        public bool StackTrace { get; set; }

        protected override string GetArguments()
        {
            if (BatchFile is null)
                throw new Exception("Missing BatchFile Property");

            var builder = new StringBuilder("batch ");
            builder.AppendFormat("\"{0}\"", BatchFile.ItemSpec);

            if (!(InputFile is null))
            {
                builder.AppendFormat(" --input \"{0}\"", InputFile.ItemSpec);
            }

            if (Reset)
            {
                builder.Append(" --reset");
                if (!(Checkpoint is null))
                {
                    builder.AppendFormat(":\"{0}\"", Checkpoint.ItemSpec);
                }
            }

            if (Trace)
            { builder.Append(" --trace"); }
            if (StackTrace)
            { builder.Append(" --stack-trace"); }

            return builder.ToString();
        }
    }
}
