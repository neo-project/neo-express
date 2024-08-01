// Copyright (C) 2015-2024 The Neo Project.
//
// TestApplicationEngine.CoverageWriter.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using System.IO.Abstractions;
using BinaryWriter = System.IO.BinaryWriter;
using ExecutionContext = Neo.VM.ExecutionContext;
using FileAccess = System.IO.FileAccess;
using FileMode = System.IO.FileMode;
using FileShare = System.IO.FileShare;
using IOException = System.IO.IOException;
using Stream = System.IO.Stream;
using StreamWriter = System.IO.StreamWriter;
using TextWriter = System.IO.TextWriter;

namespace Neo.BlockchainToolkit.SmartContract
{
    public partial class TestApplicationEngine
    {
        class CoverageWriter : IDisposable
        {
            readonly string coveragePath;
            readonly Stream stream;
            readonly TextWriter writer;
            readonly IFileSystem fileSystem;
            readonly Dictionary<UInt160, UInt160> scriptHashCache = new();
            bool disposed = false;

            public CoverageWriter(string coveragePath, IFileSystem? fileSystem = null)
            {
                this.fileSystem = fileSystem ?? new FileSystem();
                this.coveragePath = coveragePath;
                if (!this.fileSystem.Directory.Exists(coveragePath))
                {
                    this.fileSystem.Directory.CreateDirectory(coveragePath);
                }
                var filename = this.fileSystem.Path.Combine(coveragePath, $"{Guid.NewGuid()}.neo-coverage");

                stream = this.fileSystem.File.Create(filename);
                writer = new StreamWriter(stream);
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    writer.Flush();
                    stream.Flush();
                    writer.Dispose();
                    stream.Dispose();
                    disposed = true;
                }
            }

            public void WriteContext(ExecutionContext? context)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(CoverageWriter));

                if (context is null)
                {
                    writer.WriteLine($"{UInt160.Zero}");
                }
                else
                {
                    // Note, ExecutionContextState.ScriptHash is the contract hash used to invoke the contract. 
                    // That value is derived from the raw script hash, the contract name and the account that deployed the contract.
                    // The raw script hash is used in coverage files so that it can be tied back to the original contract
                    // independently of the deployment account.  

                    var state = context.GetState<ExecutionContextState>();
                    if (!scriptHashCache.TryGetValue(state.ScriptHash, out var hash))
                    {
                        hash = context.Script.CalculateScriptHash();
                        scriptHashCache.Add(state.ScriptHash, hash);
                    }

                    writer.WriteLine($"{hash}");

                    if (state.Contract?.Nef is null)
                    {
                        var scriptPath = fileSystem.Path.Combine(coveragePath, $"{hash}.neo-script");
                        WriteScriptFile(scriptPath, stream => stream.Write(context.Script.AsSpan()));
                    }
                    else
                    {
                        var scriptPath = fileSystem.Path.Combine(coveragePath, $"{hash}.nef");
                        WriteScriptFile(scriptPath, stream =>
                        {
                            var writer = new BinaryWriter(stream);
                            state.Contract.Nef.Serialize(writer);
                            writer.Flush();
                        });
                    }
                }
            }

            void WriteScriptFile(string filename, Action<Stream> writeFileAction)
            {
                if (!fileSystem.File.Exists(filename))
                {
                    try
                    {
                        using var stream = fileSystem.File.Open(filename, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        writeFileAction(stream);
                        stream.Flush();
                    }
                    // ignore IOException thrown because file already exists
                    catch (IOException) { }
                }
            }

            // WriteAddress and WriteBranch do not need disposed check since writer will be disposed
            public void WriteAddress(int ip) => writer.WriteLine($"{ip}");

            public void WriteBranch(int ip, int offset, int result) => writer.WriteLine($"{ip} {offset} {result}");
        }
    }
}
