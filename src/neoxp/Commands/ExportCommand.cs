// Copyright (C) 2015-2026 The Neo Project.
//
// ExportCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Extensions;
using NeoExpress.Models;
using Newtonsoft.Json;
using System.IO.Abstractions;

namespace NeoExpress.Commands
{
    [Command("export", Description = "Export neo-express protocol, config and wallet files")]
    class ExportCommand
    {
        readonly ExpressChainManagerFactory chainManagerFactory;
        readonly IFileSystem fileSystem;

        public ExportCommand(ExpressChainManagerFactory chainManagerFactory, IFileSystem fileSystem)
        {
            this.chainManagerFactory = chainManagerFactory;
            this.fileSystem = fileSystem;
        }

        [Option(Description = "Path to neo-express data file")]
        internal string Input { get; init; } = string.Empty;

        [Option(Description = "Password to use for the exported wallets (prompted for if unspecified)")]
        internal string Password { get; init; } = string.Empty;

        internal void Execute(System.IO.TextWriter writer)
        {
            var password = Extensions.ResolveExportPassword(Password, Console.IsInputRedirected,
                () => Prompt.GetPassword("Input password to use for exported wallets"));
            var (chainManager, _) = chainManagerFactory.LoadChain(Input);
            var chain = chainManager.Chain;
            var folder = fileSystem.Directory.GetCurrentDirectory();

            for (var i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var node = chain.ConsensusNodes[i];
                writer.WriteLine($"Exporting {node.Wallet.Name} Consensus Node config + wallet");
                ExportNode(chainManager.ProtocolSettings, node, folder, password, writer);
            }
        }

        // Stages the wallet/config pair in temporary files, then replaces the outputs with rollback.
        // Replacing two files is not atomic; backups are kept and reported if rollback fails.
        internal void ExportNode(ProtocolSettings settings, ExpressConsensusNode node, string folder, string password, System.IO.TextWriter? warnings = null)
        {
            var walletPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.wallet.json");
            var configPath = fileSystem.Path.Combine(folder, $"{node.Wallet.Name}.config.json");
            var walletTemp = TempPath(walletPath, "tmp");
            var configTemp = TempPath(configPath, "tmp");
            var walletBackup = TempPath(walletPath, "bak");
            var configBackup = TempPath(configPath, "bak");

            try
            {
                StageWallet(settings, node, walletTemp, password);
                StageConfig(settings, node, configTemp, password, walletPath);

                if (fileSystem.Directory.Exists(walletPath))
                    throw new System.IO.IOException($"Output path '{walletPath}' is a directory");
                if (fileSystem.Directory.Exists(configPath))
                    throw new System.IO.IOException($"Output path '{configPath}' is a directory");

                foreach (var leftover in Replace((walletPath, walletTemp, walletBackup), (configPath, configTemp, configBackup)))
                    warnings?.WriteLine($"Warning: export succeeded but previous file backup could not be deleted and may contain sensitive data: {leftover}");
            }
            finally
            {
                TryDelete(walletTemp);
                TryDelete(configTemp);
            }
        }

        string TempPath(string path, string extension)
            => fileSystem.Path.Combine(
                fileSystem.Path.GetDirectoryName(path) ?? fileSystem.Directory.GetCurrentDirectory(),
                $".{fileSystem.Path.GetFileName(path)}.{Guid.NewGuid():N}.{extension}");

        bool TryDelete(string path)
        {
            try
            {
                if (fileSystem.File.Exists(path))
                    fileSystem.File.Delete(path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        List<string> Replace(params (string Path, string Temp, string Backup)[] files)
        {
            var movedAside = new List<(string Path, string Backup)>();
            var placed = new List<string>();
            try
            {
                foreach (var (path, _, backup) in files)
                {
                    if (!fileSystem.File.Exists(path))
                        continue;
                    fileSystem.File.Move(path, backup);
                    movedAside.Add((path, backup));
                }

                foreach (var (path, temp, _) in files)
                {
                    fileSystem.File.Move(temp, path);
                    placed.Add(path);
                }
            }
            catch (Exception ex)
            {
                var unrestored = new List<string>();
                foreach (var path in placed)
                    TryDelete(path);
                foreach (var (path, backup) in movedAside)
                {
                    try
                    {
                        fileSystem.File.Move(backup, path);
                    }
                    catch (Exception)
                    {
                        unrestored.Add($"'{backup}' (original '{path}')");
                    }
                }

                if (unrestored.Count > 0)
                    throw new System.IO.IOException(
                        $"Export failed and rollback was incomplete. Original files preserved at: {string.Join(", ", unrestored)}", ex);
                throw;
            }

            var leftovers = new List<string>();
            foreach (var (_, backup) in movedAside)
            {
                if (!TryDelete(backup))
                    leftovers.Add(backup);
            }
            return leftovers;
        }

        internal int OnExecute(CommandLineApplication app, IConsole console)
        {
            try
            {
                Execute(console.Out);
                return 0;
            }
            catch (Exception ex)
            {
                app.WriteException(ex);
                return 1;
            }
        }

        void StageWallet(ProtocolSettings settings, ExpressConsensusNode node, string tempPath, string password)
        {
            var devWallet = DevWallet.FromExpressWallet(settings, node.Wallet);
            devWallet.Export(tempPath, password);
            if (!fileSystem.File.Exists(tempPath))
                throw new System.IO.IOException("Wallet export did not produce a file");
        }

        void StageConfig(ProtocolSettings settings, ExpressConsensusNode node, string path, string password, string walletPath)
        {
            using (var stream = fileSystem.File.Open(path, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write))
            using (var streamWriter = new System.IO.StreamWriter(stream))
            using (var writer = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented })
            {
                WriteNodeConfig(writer, settings, node, password, walletPath);
            }
        }

        static void WriteNodeConfig(JsonTextWriter writer, ProtocolSettings settings, ExpressConsensusNode node, string password, string walletPath)
        {

            // use neo-cli defaults for Logger & Storage

            writer.WriteStartObject();
            writer.WritePropertyName("ApplicationConfiguration");
            writer.WriteStartObject();

            writer.WritePropertyName("Storage");
            writer.WriteStartObject();
            writer.WritePropertyName("Engine");
            writer.WriteValue("MemoryStore");
            writer.WriteEndObject();

            writer.WritePropertyName("P2P");
            writer.WriteStartObject();
            writer.WritePropertyName("Port");
            writer.WriteValue(node.TcpPort);
            writer.WriteEndObject();

            writer.WritePropertyName("UnlockWallet");
            writer.WriteStartObject();
            writer.WritePropertyName("Path");
            writer.WriteValue(walletPath);
            writer.WritePropertyName("Password");
            writer.WriteValue(password);
            writer.WritePropertyName("IsActive");
            writer.WriteValue(true);
            writer.WriteEndObject();

            writer.WriteEndObject();

            WriteProtocolConfiguration(writer, settings);

            writer.WriteEndObject();
        }

        internal static void WriteProtocolConfiguration(JsonTextWriter writer, ProtocolSettings settings)
        {
            writer.WritePropertyName("ProtocolConfiguration");
            writer.WriteStartObject();

            writer.WritePropertyName("Network");
            writer.WriteValue(settings.Network);
            writer.WritePropertyName("AddressVersion");
            writer.WriteValue(settings.AddressVersion);
            writer.WritePropertyName("MillisecondsPerBlock");
            writer.WriteValue(settings.MillisecondsPerBlock);
            writer.WritePropertyName("MaxTransactionsPerBlock");
            writer.WriteValue(settings.MaxTransactionsPerBlock);
            writer.WritePropertyName("MemoryPoolMaxTransactions");
            writer.WriteValue(settings.MemoryPoolMaxTransactions);
            writer.WritePropertyName("MaxTraceableBlocks");
            writer.WriteValue(settings.MaxTraceableBlocks);
            writer.WritePropertyName("MaxValidUntilBlockIncrement");
            writer.WriteValue(settings.MaxValidUntilBlockIncrement);
            writer.WritePropertyName("InitialGasDistribution");
            writer.WriteValue(settings.InitialGasDistribution);
            writer.WritePropertyName("ValidatorsCount");
            writer.WriteValue(settings.ValidatorsCount);

            writer.WritePropertyName("Hardforks");
            writer.WriteStartObject();
            foreach (var hardfork in settings.Hardforks.OrderBy(pair => pair.Key))
            {
                writer.WritePropertyName(hardfork.Key.ToString());
                writer.WriteValue(hardfork.Value);
            }
            writer.WriteEndObject();

            writer.WritePropertyName("StandbyCommittee");
            writer.WriteStartArray();
            foreach (var publicKey in settings.StandbyCommittee)
            {
                writer.WriteValue(publicKey.EncodePoint(true).ToHexString());
            }
            writer.WriteEndArray();

            writer.WritePropertyName("SeedList");
            writer.WriteStartArray();
            foreach (var seed in settings.SeedList)
            {
                writer.WriteValue(seed);
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}
