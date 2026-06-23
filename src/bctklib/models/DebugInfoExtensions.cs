// Copyright (C) 2015-2026 The Neo Project.
//
// DebugInfoExtensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.BlockchainToolkit.Models
{
    /// <summary>A source breakpoint bound to a concrete NeoVM instruction address.</summary>
    /// <param name="Address">The instruction pointer the breakpoint binds to.</param>
    /// <param name="Line">The source line the breakpoint actually bound to (1-based).</param>
    /// <param name="Column">The source column the bound sequence point starts at (1-based).</param>
    /// <param name="Document">The index into <see cref="DebugInfo.Documents"/> of the bound document.</param>
    public readonly record struct ResolvedBreakpoint(int Address, int Line, int Column, int Document);

    /// <summary>
    /// Maps source-level breakpoints (a document path + line number) onto the NeoVM instruction
    /// addresses recorded in a contract's <see cref="DebugInfo"/>. This is the source-map lookup a
    /// source-level debugger performs when an editor sets a breakpoint.
    /// </summary>
    public static class DebugInfoExtensions
    {
        /// <summary>
        /// Resolves a source breakpoint to the NeoVM instruction it should stop on. A breakpoint set on
        /// a line that carries no sequence point (a blank line, a brace, a comment) snaps forward to the
        /// next sequence point in the same document, matching how mainstream debuggers bind breakpoints.
        /// </summary>
        /// <param name="debugInfo">The contract debug information to search.</param>
        /// <param name="source">The source document path the breakpoint was set in.</param>
        /// <param name="line">The 1-based source line the breakpoint was set on.</param>
        /// <param name="resolved">The bound breakpoint, when this method returns <see langword="true"/>.</param>
        /// <returns><see langword="true"/> if a sequence point at or after <paramref name="line"/> was found.</returns>
        public static bool TryResolveBreakpoint(this DebugInfo debugInfo, string source, int line, out ResolvedBreakpoint resolved)
        {
            ArgumentNullException.ThrowIfNull(debugInfo);

            ResolvedBreakpoint? best = null;
            for (var document = 0; document < debugInfo.Documents.Count; document++)
            {
                if (!DocumentMatches(debugInfo.Documents[document], source))
                    continue;

                foreach (var method in debugInfo.Methods)
                {
                    foreach (var sp in method.SequencePoints)
                    {
                        if (sp.Document != document || sp.Start.Line < line)
                            continue;

                        // Prefer the earliest line at or after the request, then the lowest address on it,
                        // so a breakpoint binds to the first instruction of the closest executable line.
                        if (best is null
                            || sp.Start.Line < best.Value.Line
                            || (sp.Start.Line == best.Value.Line && sp.Address < best.Value.Address))
                        {
                            best = new ResolvedBreakpoint(sp.Address, sp.Start.Line, sp.Start.Column, document);
                        }
                    }
                }
            }

            resolved = best ?? default;
            return best is not null;
        }

        /// <summary>
        /// Resolves a source breakpoint to the NeoVM instruction it should stop on, or <see langword="null"/>
        /// when the document carries no sequence point at or after <paramref name="line"/>.
        /// </summary>
        public static ResolvedBreakpoint? ResolveBreakpoint(this DebugInfo debugInfo, string source, int line)
            => debugInfo.TryResolveBreakpoint(source, line, out var resolved) ? resolved : null;

        static bool DocumentMatches(string document, string requested)
        {
            var normalizedDocument = Normalize(document);
            var normalizedRequested = Normalize(requested);
            if (string.Equals(normalizedDocument, normalizedRequested, StringComparison.OrdinalIgnoreCase))
                return true;

            // Fall back to a file-name match so a breakpoint still binds when the editor and the debug
            // info disagree on the absolute path (a common case with relocated or source-mapped trees).
            return string.Equals(
                FileName(normalizedDocument),
                FileName(normalizedRequested),
                StringComparison.OrdinalIgnoreCase);
        }

        // Both separators are handled explicitly: debug info produced on Windows carries '\' paths that
        // Path.GetFileName would not split on a Unix host, so the comparison is normalized first.
        static string Normalize(string path) => path.Replace('\\', '/');

        static string FileName(string normalizedPath)
        {
            var slash = normalizedPath.LastIndexOf('/');
            return slash < 0 ? normalizedPath : normalizedPath[(slash + 1)..];
        }
    }
}
