using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Neo.Express.Abstractions
{
    public interface INeoBackend
    {
        void CreateBlockchain(string filename, int count, ushort Port);
        CancellationTokenSource RunBlockchain(string filename, string storeFolder, int? index, uint secondsPerBlock, bool reset, Action<string> consoleWrite);
    }
}
