// Copyright (C) 2015-2026 The Neo Project.
//
// StopCommand.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo.Network.RPC;
using System.IO.Abstractions;

namespace NeoWorkNet.Commands;

[Command("stop", Description = "Stop the running Neo-WorkNet instance node")]
class StopCommand
{
    // must match the port hardcoded in RunCommand.GetRpcServerSettings
    internal const int RPC_PORT = 30332;

    readonly IFileSystem fs;

    public StopCommand(IFileSystem fs)
    {
        this.fs = fs;
    }

    internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
    {
        try
        {
            var (_, worknet) = await fs.LoadWorknetAsync(app).ConfigureAwait(false);

            using var rpcClient = new RpcClient(new Uri($"http://localhost:{RPC_PORT}"),
                protocolSettings: worknet.BranchInfo.ProtocolSettings);
            Neo.Json.JToken json;
            try
            {
                json = await rpcClient.RpcSendAsync("expressshutdown").ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                await console.Out.WriteLineAsync("worknet node was not running").ConfigureAwait(false);
                return 0;
            }

            var processId = int.Parse(json["process-id"]!.AsString());
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(processId);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
            catch (ArgumentException)
            {
                // the process exited before we could observe it
            }
            await console.Out.WriteLineAsync("worknet node stopped").ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            app.WriteException(ex);
            return 1;
        }
    }
}
