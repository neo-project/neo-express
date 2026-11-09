// Copyright (C) 2015-2026 The Neo Project.
//
// NeoExpressBatch.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Neo.BuildTasks
{
    public class NeoExpressBatch : DotNetToolTask
    {
        const string PACKAGE_ID = "Neo.Express";
        const string COMMAND = "neoxp";
        const string RunningChainMessage = "Cannot run batch command while blockchain is running";

        protected override string Command => COMMAND;
        protected override string PackageId => PACKAGE_ID;

        [Required]
        public ITaskItem? BatchFile { get; set; }

        public ITaskItem? InputFile { get; set; }

        public bool Reset { get; set; }

        public ITaskItem? Checkpoint { get; set; }

        public bool Trace { get; set; }

        public bool StackTrace { get; set; }

        public override bool Execute()
        {
            if (BatchFile is null)
                throw new Exception("Missing BatchFile Property");

            var directory = WorkingDirectory;
            if (!FindTool(PackageId, directory, out var resolvedType, out var version))
            {
                Log.LogError("tool package {0} not found", PackageId);
                return false;
            }

            SetResolvedToolType(resolvedType);
            ToolVersion = version.ToString();
            Log.LogMessage(MessageImportance.High, "Found {0} tool package {1} version {2}",
                resolvedType, PackageId, version);

            var command = resolvedType == DotNetToolType.Global ? Command : "dotnet";
            var batchArguments = resolvedType == DotNetToolType.Global
                ? GetArguments()
                : Command + " " + GetArguments();
            CommandLine = $"{command} {batchArguments}";
            Log.LogMessage(MessageImportance.High, "Running {0} {1}", command, batchArguments);

            if (TryExecute(command, batchArguments, directory, out var output, logFailure: false))
            {
                ExecutionSuccess(output);
                return true;
            }

            if (IsChainRunningFailure(LastProcessResults))
            {
                Log.LogMessage(MessageImportance.High,
                    "Neo Express is running; applying deploy lines from {0} without resetting the chain.",
                    BatchFile.ItemSpec);
                return DeployContractsWhileRunning(command, resolvedType, directory);
            }

            Log.LogError("{0} returned {1}", Command, LastProcessResults.ExitCode);
            foreach (var err in LastProcessResults.Error)
            {
                Log.LogError(err);
            }
            foreach (var @out in LastProcessResults.Output)
            {
                Log.LogWarning(@out);
            }
            return false;
        }

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

        internal static bool IsChainRunningFailure(ProcessResults results)
        {
            return results.Error.Concat(results.Output)
                .Any(line => line.IndexOf(RunningChainMessage, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static IReadOnlyList<string> ParseExecutableLines(string batchText)
        {
            var lines = new List<string>();
            using var reader = new StringReader(batchText);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                    continue;
                lines.Add(trimmed);
            }
            return lines;
        }

        internal static IReadOnlyList<string> SplitCommandLine(string commandLine)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < commandLine.Length; i++)
            {
                var c = commandLine[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                    continue;
                }
                current.Append(c);
            }
            if (inQuotes)
                throw new FormatException("Unbalanced quote in batch command line.");
            if (current.Length > 0)
                tokens.Add(current.ToString());
            return tokens;
        }

        internal static bool IsContractDeployLine(IReadOnlyList<string> parts)
        {
            return parts.Count >= 4
                && parts[0].Equals("contract", StringComparison.OrdinalIgnoreCase)
                && parts[1].Equals("deploy", StringComparison.OrdinalIgnoreCase);
        }

        internal static string QuoteArgument(string argument)
        {
            if (argument.Length == 0)
                return "\"\"";
            var needsQuotes = false;
            for (var i = 0; i < argument.Length; i++)
            {
                if (char.IsWhiteSpace(argument[i]) || argument[i] == '"')
                {
                    needsQuotes = true;
                    break;
                }
            }
            if (!needsQuotes)
                return argument;
            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }

        internal static bool ContainsOption(IReadOnlyList<string> parts, params string[] names)
        {
            for (var i = 0; i < parts.Count; i++)
            {
                for (var n = 0; n < names.Length; n++)
                {
                    if (parts[i].Equals(names[n], StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        internal static string BuildDeployCommand(IReadOnlyList<string> parts, string? inputFile, bool trace)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                    builder.Append(' ');
                builder.Append(QuoteArgument(parts[i]));
            }
            if (!string.IsNullOrEmpty(inputFile) && !ContainsOption(parts, "--input", "-i"))
            {
                builder.Append(" --input ");
                builder.Append(QuoteArgument(inputFile!));
            }
            if (trace && !ContainsOption(parts, "--trace"))
                builder.Append(" --trace");
            return builder.ToString();
        }

        bool DeployContractsWhileRunning(string command, DotNetToolType resolvedType, ITaskItem? directory)
        {
            var batchPath = BatchFile!.ItemSpec;
            if (!Path.IsPathRooted(batchPath) && directory is not null && !string.IsNullOrEmpty(directory.ItemSpec))
            {
                batchPath = Path.Combine(directory.ItemSpec, batchPath);
            }
            if (!File.Exists(batchPath))
            {
                Log.LogError("Batch file \"{0}\" could not be found", batchPath);
                return false;
            }

            IReadOnlyList<string> lines;
            try
            {
                lines = ParseExecutableLines(File.ReadAllText(batchPath));
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to read batch file \"{0}\": {1}", batchPath, ex.Message);
                return false;
            }

            var deployLines = new List<IReadOnlyList<string>>();
            foreach (var line in lines)
            {
                IReadOnlyList<string> parts;
                try
                {
                    parts = SplitCommandLine(line);
                }
                catch (FormatException ex)
                {
                    Log.LogError("{0}: {1}", batchPath, ex.Message);
                    return false;
                }

                if (IsContractDeployLine(parts))
                {
                    deployLines.Add(parts);
                    continue;
                }

                Log.LogError(
                    "Neo Express is running; batch file {0} also contains \"{1}\", which cannot be applied while the chain is up. Stop the node and rerun the batch, or keep only 'contract deploy' lines.",
                    batchPath,
                    line);
                return false;
            }

            if (deployLines.Count == 0)
            {
                Log.LogError("Neo Express is running, and {0} has no 'contract deploy' lines to apply.", batchPath);
                return false;
            }

            foreach (var parts in deployLines)
            {
                var builder = new StringBuilder();
                if (resolvedType != DotNetToolType.Global)
                {
                    builder.Append(Command);
                    builder.Append(' ');
                }
                builder.Append(BuildDeployCommand(parts, InputFile?.ItemSpec, Trace));

                var arguments = builder.ToString();
                Log.LogMessage(MessageImportance.High, "Running {0} {1}", command, arguments);
                if (!TryExecute(command, arguments, directory, out var output))
                {
                    return false;
                }
                ExecutionSuccess(output);
            }

            return true;
        }
    }
}
