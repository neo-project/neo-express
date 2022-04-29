using System.IO.Abstractions;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;

namespace NeoExpress
{
    class ExpressFile : IExpressFile
    {
        readonly IFileSystem fileSystem;
        readonly string chainPath;

        public ExpressChain Chain { get; }

        public ExpressFile(string input, IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
            (Chain, chainPath) = fileSystem.LoadExpressChain(input);
        }

        public void Save()
        {
            fileSystem.SaveChain(Chain, chainPath);
        }

        public IExpressNode GetExpressNode(bool offlineTrace = false)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            for (int i = 0; i < Chain.ConsensusNodes.Count; i++)
            {
                var consensusNode = Chain.ConsensusNodes[i];
                if (consensusNode.IsRunning())
                {
                    return new Node.OnlineNode(Chain, consensusNode);
                }
            }

            var node = Chain.ConsensusNodes[0];
            var nodePath = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(nodePath)) fileSystem.Directory.CreateDirectory(nodePath);
            var provider = RocksDbStorageProvider.Open(nodePath);
            return new Node.OfflineNode(Chain, node, provider, offlineTrace);
        }
    }
}
