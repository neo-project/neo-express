namespace NeoExpress
{
    internal interface IExpressChainManagerFactory
    {
        (IExpressChainManager manager, string path) CreateChain(int nodeCount, string outputPath, bool force);
        (IExpressChainManager manager, string path) LoadChain(string path);
    }
}
