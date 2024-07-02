// Copyright (C) 2015-2024 The Neo Project.
//
// DebugInfo.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneOf;
using System.Collections.Immutable;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Text.RegularExpressions;
using NotFound = OneOf.Types.NotFound;

namespace Neo.BlockchainToolkit.Models
{
    public record DebugInfo(
        UInt160 ScriptHash,
        string DocumentRoot,
        IReadOnlyList<string> Documents,
        IReadOnlyList<DebugInfo.Method> Methods,
        IReadOnlyList<DebugInfo.Event> Events,
        IReadOnlyList<DebugInfo.SlotVariable> StaticVariables)
    {
        public readonly record struct Event(
            string Id,
            string Namespace,
            string Name,
            IReadOnlyList<SlotVariable> Parameters);

        public readonly record struct Method(
            string Id,
            string Namespace,
            string Name,
            (int Start, int End) Range,
            string ReturnType,
            IReadOnlyList<SlotVariable> Parameters,
            IReadOnlyList<SlotVariable> Variables,
            IReadOnlyList<SequencePoint> SequencePoints);

        public readonly record struct SequencePoint(
            int Address,
            int Document,
            (int Line, int Column) Start,
            (int Line, int Column) End);

        public readonly record struct SlotVariable(
            string Name,
            string Type,
            int Index);

        const string NEF_DBG_NFO_EXTENSION = ".nefdbgnfo";
        const string DEBUG_JSON_EXTENSION = ".debug.json";
        static readonly Regex spRegex = new(@"^(\d+)\[(-?\d+)\](\d+)\:(\d+)\-(\d+)\:(\d+)$");

        public static DebugInfo Parse(JObject json)
        {
            var hash = json.TryGetValue("hash", out var hashToken)
                ? UInt160.Parse(hashToken.Value<string>() ?? "")
                : throw new FormatException("Missing hash value");

            var docRoot = json.TryGetValue("document-root", out var docRootToken)
                ? docRootToken.Value<string>() ?? ""
                : "";

            var documents = json["documents"]?.Select(token => token.Value<string>() ?? "").ToArray() ?? Array.Empty<string>();
            var events = json["events"]?.Select(ParseEvent).ToArray() ?? Array.Empty<Event>();
            var methods = json["methods"]?.Select(ParseMethod).ToArray() ?? Array.Empty<Method>();
            var staticVars = ParseSlotVariables(json["static-variables"]).ToArray();

            return new DebugInfo(hash, docRoot, documents, methods, events, staticVars);
        }

        static IEnumerable<SlotVariable> ParseSlotVariables(JToken? token)
        {
            if (token is null)
                return Enumerable.Empty<SlotVariable>();
            var vars = token.Select(ParseType).ToList();

            if (vars.Any(t => t.slotIndex.HasValue) && !vars.All(t => t.slotIndex.HasValue))
            {
                throw new FormatException("cannot mix and match optional slot index information");
            }

            return vars.Select((v, i) => new SlotVariable(v.name, v.type, v.slotIndex!.HasValue ? v.slotIndex.Value : i));

            static (string name, string type, int? slotIndex) ParseType(JToken token)
            {
                var value = token.Value<string>() ?? throw new FormatException("invalid type token");
                var values = value.Split(',');
                if (values.Length == 2)
                {
                    return (values[0], values[1], null);
                }
                if (values.Length == 3
                    && int.TryParse(values[2], out var slotIndex)
                    && slotIndex >= 0)
                {

                    return (values[0], values[1], slotIndex);
                }

                throw new FormatException($"invalid type string \"{value}\"");
            }
        }

        static Event ParseEvent(JToken token)
        {
            var id = token.Value<string>("id") ?? throw new FormatException("Invalid event id");
            var (@namespace, name) = ParseName(token["name"]);
            var @params = ParseSlotVariables(token["params"]).ToImmutableList();

            return new Event(id, name, @namespace, @params);
        }

        static SequencePoint ParseSequencePoint(JToken token)
        {
            var value = token.Value<string>() ?? throw new FormatException("invalid Sequence Point token");
            var match = spRegex.Match(value);
            if (match.Groups.Count != 7)
                throw new FormatException($"Invalid Sequence Point \"{value}\"");

            var address = int.Parse(match.Groups[1].Value);
            var document = int.Parse(match.Groups[2].Value);
            var start = (int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value));
            var end = (int.Parse(match.Groups[5].Value), int.Parse(match.Groups[6].Value));

            return new SequencePoint(address, document, start, end);
        }

        static Method ParseMethod(JToken token)
        {
            var id = token.Value<string>("id") ?? throw new FormatException("Invalid method id");
            var (@namespace, name) = ParseName(token["name"]);
            var @return = token.Value<string>("return") ?? "Void";
            var @params = ParseSlotVariables(token["params"]).ToArray();
            var variables = ParseSlotVariables(token["variables"]).ToArray();
            var sequencePoints = token["sequence-points"]?.Select(ParseSequencePoint).ToArray()
                ?? Array.Empty<SequencePoint>();
            var range = ParseRange(token["range"]);

            return new Method(id, name, @namespace, range, @return, @params, variables, sequencePoints);
        }

        static (string, string) ParseName(JToken? token)
        {
            var name = token?.Value<string>() ?? throw new FormatException("Missing name");
            var values = name.Split(',') ?? throw new FormatException($"Invalid name '{name}'");
            return values.Length == 2
                ? (values[0], values[1])
                : throw new FormatException($"Invalid name '{name}'");
        }

        static (int, int) ParseRange(JToken? token)
        {
            var range = token?.Value<string>() ?? throw new FormatException("Missing range");
            var values = range.Split('-') ?? throw new FormatException($"Invalid range '{range}'");
            return values.Length == 2
                ? (int.Parse(values[0]), int.Parse(values[1]))
                : throw new FormatException($"Invalid range '{range}'");
        }

        [Obsolete($"use {nameof(LoadContractDebugInfoAsync)} instead")]
        public static Task<OneOf<DebugInfo, NotFound>> LoadAsync(string nefFileName, IReadOnlyDictionary<string, string>? sourceFileMap = null, IFileSystem? fileSystem = null)
            => LoadContractDebugInfoAsync(nefFileName, sourceFileMap, fileSystem);

        public static async Task<OneOf<DebugInfo, NotFound>> LoadContractDebugInfoAsync(string nefFileName, IReadOnlyDictionary<string, string>? sourceFileMap = null, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();

            DebugInfo debugInfo;
            var compressedFileName = fileSystem.Path.ChangeExtension(nefFileName, NEF_DBG_NFO_EXTENSION);
            var uncompressedFileName = fileSystem.Path.ChangeExtension(nefFileName, DEBUG_JSON_EXTENSION);
            if (fileSystem.File.Exists(compressedFileName))
            {
                using var stream = fileSystem.File.OpenRead(compressedFileName);
                debugInfo = await LoadCompressedAsync(stream).ConfigureAwait(false);
            }
            else if (fileSystem.File.Exists(uncompressedFileName))
            {
                using var stream = fileSystem.File.OpenRead(uncompressedFileName);
                debugInfo = await LoadAsync(stream).ConfigureAwait(false);
            }
            else
            {
                return default(NotFound);
            }

            var resolvedDocuments = ResolveDocuments(debugInfo, sourceFileMap, fileSystem).ToArray();
            return debugInfo with { Documents = resolvedDocuments };
        }

        public static async Task<DebugInfo> LoadAsync(string fileName, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();

            var extension = fileSystem.Path.GetExtension(fileName);
            if (extension == NEF_DBG_NFO_EXTENSION)
            {
                using var stream = fileSystem.File.OpenRead(fileName);
                return await LoadCompressedAsync(stream).ConfigureAwait(false);
            }
            else if (extension == DEBUG_JSON_EXTENSION)
            {
                using var stream = fileSystem.File.OpenRead(fileName);
                return await LoadAsync(stream).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentException($"Invalid Debug Info extension {extension}", nameof(fileName));
            }
        }

        internal static async Task<DebugInfo> LoadCompressedAsync(System.IO.Stream stream)
        {
            using var archive = new ZipArchive(stream);
            using var entryStream = archive.Entries[0].Open();
            return await LoadAsync(entryStream).ConfigureAwait(false);
        }

        internal static async Task<DebugInfo> LoadAsync(System.IO.Stream stream)
        {
            using var streamReader = new System.IO.StreamReader(stream);
            using var jsonReader = new JsonTextReader(streamReader);
            var root = await JObject.LoadAsync(jsonReader).ConfigureAwait(false);
            return Parse(root);
        }

        public static IEnumerable<string> ResolveDocuments(DebugInfo debugInfo, IReadOnlyDictionary<string, string>? sourceFileMap = null, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();
            var sourceMap = sourceFileMap?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new();
            return debugInfo.Documents.Select(doc => ResolveDocument(doc, sourceMap, fileSystem));
        }

        internal static string ResolveDocument(string document, IDictionary<string, string> sourceFileMap, IFileSystem fileSystem)
        {
            if (fileSystem.File.Exists(document))
                return document;

            foreach (var (key, value) in sourceFileMap)
            {
                if (document.StartsWith(key))
                {
                    var remainder = FileNameUtilities.TrimStartDirectorySeparators(document[key.Length..]);
                    var mappedDoc = fileSystem.NormalizePath(fileSystem.Path.Join(value, remainder));
                    if (fileSystem.File.Exists(mappedDoc))
                    {
                        return mappedDoc;
                    }
                }
            }

            var cwd = fileSystem.Directory.GetCurrentDirectory();
            var cwdDocument = fileSystem.Path.Join(cwd, FileNameUtilities.GetFileName(document));
            if (fileSystem.File.Exists(cwdDocument))
            {
                var directoryName = FileNameUtilities.GetDirectoryName(document);
                if (directoryName != null)
                {
                    sourceFileMap.Add(directoryName, cwd);
                }

                return cwdDocument;
            }

            var folderName = FileNameUtilities.GetFileName(cwd);
            var folderIndex = document.IndexOf(folderName);
            if (folderIndex >= 0)
            {
                var relPath = document[(folderIndex + folderName.Length)..];
                var newPath = fileSystem.Path.GetFullPath(fileSystem.Path.Join(cwd, relPath));

                if (fileSystem.File.Exists(newPath))
                {
                    return newPath;
                }
            }

            return document;
        }
    }
}
