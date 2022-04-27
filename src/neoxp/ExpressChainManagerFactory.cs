using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.SmartContract;
using Neo.Wallets;
using NeoExpress.Models;
using static Neo.BlockchainToolkit.Constants;

namespace NeoExpress
{
    internal class ExpressChainManagerFactory
    {
        readonly IFileSystem fileSystem;

        public ExpressChainManagerFactory(IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        string ResolveChainFileName(string path) => fileSystem.ResolveFileName(path, EXPRESS_EXTENSION, () => DEFAULT_EXPRESS_FILENAME);

        public (ExpressChainManager manager, string path) LoadChain(string path, uint? secondsPerBlock = null)
        {
            path = ResolveChainFileName(path);
            if (!fileSystem.File.Exists(path))
            {
                throw new Exception($"{path} file doesn't exist");
            }

            var chain = fileSystem.LoadChain(path);

            // validate neo-express file by ensuring stored node zero default account SignatureRedeemScript matches a generated script
            var account = chain.ConsensusNodes[0].Wallet.DefaultAccount ?? throw new InvalidOperationException("consensus node 0 missing default account");
            var keyPair = new KeyPair(account.PrivateKey.HexToBytes());
            var contractScript = account.Contract.Script.HexToBytes();

            if (!Contract.CreateSignatureRedeemScript(keyPair.PublicKey).AsSpan().SequenceEqual(contractScript))
            {
                throw new Exception("Invalid Signature Redeem Script. Was this neo-express file created before RC1?");
            }

            return (new ExpressChainManager(fileSystem, chain, secondsPerBlock), path);
        }
    }
}
