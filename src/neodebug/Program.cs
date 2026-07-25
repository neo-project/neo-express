// Copyright (C) 2015-2026 The Neo Project.
//
// Program.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;

namespace NeoDebug.Neo3
{
    /// <summary>
    /// The <c>neodebug</c> tool: a stdio Debug Adapter Protocol host. An editor launches it and speaks DAP
    /// over standard in/out; each launch request is turned into a debug session by <see cref="LaunchConfigParser"/>.
    /// </summary>
    [Command("neodebug", Description = "Source-level Debug Adapter Protocol host for Neo N3 smart contracts")]
    [VersionOption(ThisAssembly.AssemblyInformationalVersion)]
    internal class Program
    {
        public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        [Option("-v|--debug-view", Description = "Default debug view: 'source' or 'disassembly'")]
        private string DefaultDebugView { get; } = string.Empty;

        private int OnExecute(CommandLineApplication app, IConsole console)
        {
            if (!TryParseDebugView(DefaultDebugView, out var defaultDebugView))
            {
                console.Error.WriteLine($"Invalid debug view '{DefaultDebugView}'. Expected 'source' or 'disassembly'.");
                return 1;
            }

            var adapter = new DebugAdapter(
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                LaunchConfigParser.CreateDebugSessionAsync,
                defaultDebugView: defaultDebugView);

            adapter.Run();
            return 0;
        }

        internal static bool TryParseDebugView(string value, out DebugView debugView)
        {
            if (string.IsNullOrEmpty(value))
            {
                debugView = DebugView.Source;
                return true;
            }

            return Enum.TryParse(value, ignoreCase: true, out debugView)
                && Enum.IsDefined(typeof(DebugView), debugView)
                && debugView is DebugView.Source or DebugView.Disassembly;
        }
    }
}
