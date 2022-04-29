using System.IO.Abstractions;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;

namespace NeoExpress
{
    public class ExpressFile : IExpressFile
    {
        readonly IFileSystem fileSystem;
        readonly string chainPath;

        public ExpressChain Chain { get; }

        public ExpressFile(string input, IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
            (Chain, chainPath) = fileSystem.LoadExpressChain(input);
        }

        public void Save()
        {
            fileSystem.SaveChain(Chain, chainPath);
        }
    }
}
