using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Neo.Network.P2P.Payloads;

namespace NeoExpress
{
    class TransactionExecutor : ITransactionExecutor
    {
        readonly IExpressChainManager chainManager;
        readonly IExpressNode expressNode;
        readonly IFileSystem fileSystem;
        readonly bool json;
        readonly System.IO.TextWriter writer;

        public TransactionExecutor(IFileSystem fileSystem, IExpressChainManager chainManager, bool trace, bool json, TextWriter writer)
        {
            this.chainManager = chainManager;
            expressNode = chainManager.GetExpressNode(trace);
            this.fileSystem = fileSystem;
            this.json = json;
            this.writer = writer;
        }

        public IExpressNode ExpressNode => expressNode;

        public Task ContractDeployAsync(string contract, string account, string password, WitnessScope witnessScope, bool force)
        {
            throw new System.NotImplementedException();
        }

        public Task ContractInvokeAsync(string invocationFile, string account, string password, WitnessScope witnessScope)
        {
            throw new System.NotImplementedException();
        }

        public void Dispose()
        {
            expressNode.Dispose();
        }

        public Task OracleEnableAsync(string account, string password)
        {
            throw new System.NotImplementedException();
        }

        public Task OracleResponseAsync(string url, string responsePath, ulong? requestId)
        {
            throw new System.NotImplementedException();
        }

        public Task TransferAsync(string quantity, string asset, string sender, string password, string receiver)
        {
            throw new System.NotImplementedException();
        }
    }
}
