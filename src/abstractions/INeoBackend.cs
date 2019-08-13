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
        //CancellationTokenSource RunBlockchain(JObject json, int index, uint secondsPerBlock, bool reset, Action<string> consoleWrite);
    }
}
