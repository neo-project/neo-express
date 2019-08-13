using System;
using System.Threading;

namespace NeoExpress.Abstractions
{
    public interface INeoBackend
    {
        ExpressChain CreateBlockchain(int count, ushort port);
        ExpressWallet CreateWallet(string name);
        CancellationTokenSource RunBlockchain(string folder, ExpressChain chain, int index, uint secondsPerBlock, Action<string> writeConsole);
        void CreateCheckpoint(ExpressChain chain, string blockchainFolder, string checkpointFolder);
        CancellationTokenSource RunCheckpoint(string directory, ExpressChain chain, uint secondsPerBlock, Action<string> writeConsole);
        void RestoreCheckpoint(ExpressChain chain, string chainDirectory, string checkPointDirectory);
    }
}
