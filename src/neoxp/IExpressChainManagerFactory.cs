namespace NeoExpress
{
    internal interface IExpressChainManagerFactory
    {
        (IExpressChainManager manager, string path) CreateChain(int nodeCount, string outputPath, bool force, uint secondsPerBlock = 0) => CreateChain(nodeCount, null, outputPath, force, secondsPerBlock);
        (IExpressChainManager manager, string path) CreateChain(int nodeCount, byte? addressVersion, string outputPath, bool force, uint secondsPerBlock = 0);
        (IExpressChainManager manager, string path) LoadChain(string path, uint? secondsPerBlock = null);
    }
}
