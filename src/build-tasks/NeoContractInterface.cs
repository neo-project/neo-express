// Copyright (C) 2015-2026 The Neo Project.
//
// NeoContractInterface.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.Build.Framework;
using System;
using System.IO;
using Task = Microsoft.Build.Utilities.Task;

namespace Neo.BuildTasks
{
    public class NeoContractInterface : Task
    {
        public override bool Execute()
        {
            if (string.IsNullOrEmpty(ManifestFile))
            {
                Log.LogError("Invalid ManifestFile " + ManifestFile);
            }
            else
            {
                try
                {
                    var manifest = NeoManifest.Load(ManifestFile);
                    var source = ContractGenerator.GenerateContractInterface(manifest, ManifestFile, ContractNameOverride, RootNamespace);
                    if (!string.IsNullOrEmpty(source))
                    {
                        // Only create a directory when OutputFile has one. Path.GetDirectoryName
                        // returns an empty string for a bare filename, which previously skipped the
                        // write entirely and left the build expecting a file that was never produced.
                        var pathFile = Path.GetDirectoryName(this.OutputFile);
                        if (!string.IsNullOrWhiteSpace(pathFile))
                        {
                            Directory.CreateDirectory(pathFile);
                        }
                        FileOperationWithRetry(() => File.WriteAllText(this.OutputFile, source));
                    }
                }
                catch (AggregateException ex)
                {
                    foreach (var inner in ex.InnerExceptions)
                    {
                        Log.LogError(inner.Message);
                    }
                }
                catch (Exception ex)
                {
                    Log.LogError(ex.Message);
                }
            }
            return !Log.HasLoggedErrors;
        }

        [Required]
        public string OutputFile { get; set; } = "";

        [Required]
        public string ManifestFile { get; set; } = "";

        public string RootNamespace { get; set; } = "";

        public string ContractNameOverride { get; set; } = "";

        internal static void FileOperationWithRetry(Action operation)
        {
            const int ProcessCannotAccessFileHR = unchecked((int)0x80070020);

            for (int retriesLeft = 6; retriesLeft > 0; retriesLeft--)
            {
                try
                {
                    operation();
                    return;
                }
                catch (IOException ex) when (ex.HResult == ProcessCannotAccessFileHR && retriesLeft > 1)
                {
                    System.Threading.Tasks.Task.Delay(100).Wait();
                    continue;
                }
            }
        }
    }
}
