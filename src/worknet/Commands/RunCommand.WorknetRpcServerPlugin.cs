using Neo;
using Neo.Plugins;

namespace NeoWorkNet.Commands;

partial class RunCommand
{
    class WorknetRpcServerPlugin : Plugin
    {
        readonly RpcServerSettings settings;
        NeoSystem? neoSystem;
        RpcServer? rpcServer;

        public WorknetRpcServerPlugin(RpcServerSettings settings)
        {
            this.settings = settings;
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (this.neoSystem is not null) throw new Exception($"{nameof(OnSystemLoaded)} already called");
            neoSystem = system;
            rpcServer = new RpcServer(system, settings);
            // rpcServer.RegisterMethods(this);
            rpcServer.StartRpcServer();

            base.OnSystemLoaded(system);
        }

        public override void Dispose()
        {
            rpcServer?.Dispose();
            base.Dispose();
        }
    }
}
