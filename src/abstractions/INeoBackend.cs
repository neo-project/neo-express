using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Neo.Express.Abstractions
{
    public interface INeoBackend
    {
        ExpressChain CreateBlockchain(int count, ushort port);
        ExpressWallet CreateWallet(string name);
        CancellationTokenSource RunBlockchain(string folder, ExpressChain chain, int index, uint secondsPerBlock, Action<string> writeConsole);
    }
}
