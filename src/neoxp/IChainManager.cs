using System.IO;
using NeoExpress.Models;

namespace NeoExpress
{
    internal interface IChainManager
    {
        ExpressChain Create(int nodeCount);
        void Export(ExpressChain chain, string password, TextWriter writer);
        bool InitializeProtocolSettings(ExpressChain chain, uint secondsPerBlock = 0);
        (ExpressChain chain, string filename) Load(string filename);
        string ResolveFileName(string filename);
        void Save(ExpressChain chain, string fileName);
    }
}
