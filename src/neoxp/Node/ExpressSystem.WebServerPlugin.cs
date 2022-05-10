using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Neo;
using Neo.BlockchainToolkit.Models;
using Neo.Plugins;

namespace NeoExpress.Node
{

    public partial class ExpressSystem
    {
        class WebServerPlugin : Neo.Plugins.Plugin
        {
            readonly RpcServerSettings rpcSettings;
            readonly CancellationTokenSource cancellationToken = new();
            NeoSystem? neoSystem = null;
            RpcServer? rpcServer = null;
            IWebHost? webHost = null;

            public CancellationToken CancellationToken => cancellationToken.Token;

            public WebServerPlugin(ExpressChain chain, ExpressConsensusNode node)
            {
                this.rpcSettings = GetRpcServerSettings(chain, node);
            }

            public void Start()
            {
                if (neoSystem is null) throw new Exception();
                if (rpcServer is null) throw new Exception();
                if (webHost is not null) throw new Exception();

                webHost = BuildWebHost(rpcSettings.BindAddress, rpcSettings.Port, rpcServer.ProcessAsync);
            }

            protected override void OnSystemLoaded(NeoSystem system)
            {
                if (neoSystem is not null)
                {
                    neoSystem = system;
                    rpcServer = new RpcServer(system, rpcSettings);
                }

                base.OnSystemLoaded(system);
            }

            static IWebHost BuildWebHost(IPAddress bindAddress, ushort port, RequestDelegate processJsonRpcRequest)
            {
                var builder = new WebHostBuilder();
                builder.UseKestrel(options =>
                {
                    options.Listen(bindAddress, port);
                });
                builder.Configure(app =>
                {
                    app.UseResponseCompression();
                    app.Run(processJsonRpcRequest);
                });
                builder.ConfigureServices(services =>
                {
                    services.AddResponseCompression(options =>
                    {
                        options.Providers.Add<GzipCompressionProvider>();
                        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append("application/json");
                    });

                    services.Configure<GzipCompressionProviderOptions>(options =>
                    {
                        options.Level = System.IO.Compression.CompressionLevel.Fastest;
                    });
                });

                var host = builder.Build();
                host.Start();
                return host;
            }

            static Neo.Plugins.RpcServerSettings GetRpcServerSettings(ExpressChain chain, ExpressConsensusNode node)
            {
                var ipAddress = chain.TryReadSetting<IPAddress>("rpc.BindAddress", IPAddress.TryParse, out var bindAddress)
                    ? bindAddress : IPAddress.Loopback;

                var settings = new Dictionary<string, string>()
                {
                    { "PluginConfiguration:Network", $"{chain.Network}" },
                    { "PluginConfiguration:BindAddress", $"{ipAddress}" },
                    { "PluginConfiguration:Port", $"{node.RpcPort}" }
                };

                if (chain.TryReadSetting<decimal>("rpc.MaxGasInvoke", decimal.TryParse, out var maxGasInvoke))
                {
                    settings.Add("PluginConfiguration:MaxGasInvoke", $"{maxGasInvoke}");
                }

                if (chain.TryReadSetting<decimal>("rpc.MaxFee", decimal.TryParse, out var maxFee))
                {
                    settings.Add("PluginConfiguration:MaxFee", $"{maxFee}");
                }

                if (chain.TryReadSetting<int>("rpc.MaxIteratorResultItems", int.TryParse, out var maxIteratorResultItems)
                    && maxIteratorResultItems > 0)
                {
                    settings.Add("PluginConfiguration:MaxIteratorResultItems", $"{maxIteratorResultItems}");
                }

                var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
                return Neo.Plugins.RpcServerSettings.Load(config.GetSection("PluginConfiguration"));
            }
        }
    }
}
