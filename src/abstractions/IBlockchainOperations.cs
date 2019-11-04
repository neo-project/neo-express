using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NeoExpress.Abstractions.Models;

namespace NeoExpress.Abstractions
{
    public interface IBlockchainOperations
    {
        ExpressChain CreateBlockchain(int count);
        void ExportBlockchain(ExpressChain chain, string folder, string password, Action<string> writeConsole);
        ExpressWallet CreateWallet(string name);
        void ExportWallet(ExpressWallet wallet, string filename, string password);
        //(byte[] signature, byte[] publicKey) Sign(ExpressWalletAccount account, byte[] data);
        void CreateCheckpoint(ExpressChain chain, string blockChainStoreDirectory, string checkPointFileName);
        void RestoreCheckpoint(ExpressChain chain, string chainDirectory, string checkPointDirectory);
        Task RunBlockchainAsync(string directory, ExpressChain chain, int index, uint secondsPerBlock, TextWriter writer, CancellationToken cancellationToken);
        Task RunCheckpointAsync(string directory, ExpressChain chain, uint secondsPerBlock, TextWriter writer, CancellationToken cancellationToken);

    }
}
