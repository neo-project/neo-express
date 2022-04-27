using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.BlockchainToolkit.Persistence;
using Neo.BlockchainToolkit.SmartContract;
using Neo.Ledger;
using Neo.Plugins;
using NeoExpress.Models;
using NeoExpress.Node;
using Nito.Disposables;

namespace NeoExpress
{
    internal class ExpressChainManager
    {

        readonly IFileSystem fileSystem;
        readonly ExpressChain chain;
        public ProtocolSettings ProtocolSettings { get; }

        public ExpressChainManager(IFileSystem fileSystem, ExpressChain chain, uint? secondsPerBlock = null)
        {
            this.fileSystem = fileSystem;
            this.chain = chain;

            uint secondsPerBlockResult = secondsPerBlock.HasValue
                ? secondsPerBlock.Value
                : chain.TryReadSetting<uint>("chain.SecondsPerBlock", uint.TryParse, out var secondsPerBlockSetting)
                    ? secondsPerBlockSetting
                    : 0;

            this.ProtocolSettings = chain.GetProtocolSettings(secondsPerBlockResult);
        }

        public ExpressChain Chain => chain;



        public IExpressNode GetExpressNode(bool offlineTrace = false)
        {
            // Check to see if there's a neo-express blockchain currently running by
            // attempting to open a mutex with the multisig account address for a name

            for (int i = 0; i < chain.ConsensusNodes.Count; i++)
            {
                var consensusNode = chain.ConsensusNodes[i];
                if (consensusNode.IsRunning())
                {
                    return new Node.OnlineNode(ProtocolSettings, chain, consensusNode);
                }
            }

            var node = chain.ConsensusNodes[0];
            var nodePath = fileSystem.GetNodePath(node);
            if (!fileSystem.Directory.Exists(nodePath)) fileSystem.Directory.CreateDirectory(nodePath);

            return new Node.OfflineNode(ProtocolSettings,
                RocksDbStorageProvider.Open(nodePath),
                node.Wallet,
                chain,
                offlineTrace);
        }
    }
}
