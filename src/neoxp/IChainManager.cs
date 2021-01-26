using System.IO;
using NeoExpress.Models;

namespace NeoExpress
{
    internal interface IChainManager
    {
        ExpressChain Create(int nodeCount);
        ExpressWallet CreateWallet(ExpressChain chain, string name);
        void Export(ExpressChain chain, string password, TextWriter writer);
        (ExpressChain chain, string filename) Load(string filename);
        string ResolveFileName(string filename);
        void Save(ExpressChain chain, string fileName);
    }
}
