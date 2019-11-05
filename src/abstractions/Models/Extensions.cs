using System;
using System.Collections.Generic;
using System.Text;

namespace NeoExpress.Abstractions.Models
{
    public static class Extensions
    {
        public static Uri GetUri(this ExpressChain chain, int node = 0) 
            => new Uri($"http://localhost:{chain.ConsensusNodes[node].RpcPort}");
    }
}
