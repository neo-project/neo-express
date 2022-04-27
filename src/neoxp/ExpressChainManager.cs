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



    }
}
