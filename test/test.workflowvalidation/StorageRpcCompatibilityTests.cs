// Copyright (C) 2015-2026 The Neo Project.
//
// StorageRpcCompatibilityTests.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using FluentAssertions;
using Neo;
using Neo.BlockchainToolkit.Models;
using NeoExpress.Node;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace test.workflowvalidation;

public class StorageRpcCompatibilityTests
{
    [Fact]
    public async Task OnlineStorageListingUsesExpressRpcEndpoint()
    {
        using var server = new StorageResponseServer();
        using var node = new OnlineNode(ProtocolSettings.Default, new ExpressChain(),
            new ExpressConsensusNode { RpcPort = (ushort)server.Port });
        var hash = UInt160.Parse("0x0102030405060708090a0b0c0d0e0f1011121314");

        var storages = await node.ListStoragesAsync(hash);

        storages.Should().Equal(("0A", "FF"), ("0B", "00"));
        using var request = JsonDocument.Parse(await server.RequestBody);
        request.RootElement.GetProperty("method").GetString()
            .Should().Be("expressgetcontractstorage");
        request.RootElement.GetProperty("params")[0].GetString()
            .Should().Be(hash.ToString());
    }

    [Fact]
    public async Task OnlineStorageListingRejectsUnexpectedPageEnvelope()
    {
        using var server = new StorageResponseServer("""{"truncated":false,"results":[]}""");
        using var node = new OnlineNode(ProtocolSettings.Default, new ExpressChain(),
            new ExpressConsensusNode { RpcPort = (ushort)server.Port });

        var action = () => node.ListStoragesAsync(UInt160.Zero);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expressgetcontractstorage*");
    }

    private sealed class StorageResponseServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly Task<string> requestTask;

        public int Port { get; }
        public Task<string> RequestBody => requestTask;

        private readonly string resultJson;

        public StorageResponseServer(string resultJson = """[{"key":"0A","value":"FF"},{"key":"0B","value":"00"}]""")
        {
            this.resultJson = resultJson;
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            requestTask = RespondAsync();
        }

        private async Task<string> RespondAsync()
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

            await reader.ReadLineAsync();
            var contentLength = 0;
            string? header;
            while (!string.IsNullOrEmpty(header = await reader.ReadLineAsync()))
            {
                if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    contentLength = int.Parse(header.Split(':', 2)[1].Trim());
            }

            var body = new char[contentLength];
            if (await reader.ReadBlockAsync(body, 0, body.Length) != body.Length)
                throw new InvalidOperationException("Incomplete RPC request body.");
            var requestBody = new string(body);
            using var request = JsonDocument.Parse(requestBody);
            var id = request.RootElement.GetProperty("id").GetRawText();
            var responseBody = $$"""{"jsonrpc":"2.0","id":{{id}},"result":{{resultJson}}}""";
            var payload = Encoding.UTF8.GetBytes(responseBody);
            var headers = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(headers);
            await stream.WriteAsync(payload);
            return requestBody;
        }

        public void Dispose()
        {
            listener.Stop();
        }
    }
}
