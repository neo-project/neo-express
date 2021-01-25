using NeoExpress.Models;

namespace NeoExpress
{
    internal interface IChainManager
    {
        string ResolveFileName(string filename);
        ExpressChain Create(int nodeCount);
        (ExpressChain chain, string filename) Load(string filename);
        bool InitializeProtocolSettings(ExpressChain chain, uint secondsPerBlock = 0);
        void Save(ExpressChain chain, string fileName);
    }
}
