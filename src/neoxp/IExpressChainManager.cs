using System.IO.Abstractions;
using NeoExpress.Models;
using Newtonsoft.Json;

namespace NeoExpress
{
    internal interface IExpressChainManager
    {
        ExpressChain Chain { get; }
        void SaveChain(string fileName);
    }

    internal class ExpressChainManager : IExpressChainManager
    {
        readonly IFileSystem fileSystem;
        readonly ExpressChain chain;

        public ExpressChainManager(IFileSystem fileSystem, ExpressChain chain)
        {
            this.fileSystem = fileSystem;
            this.chain = chain;
        }

        public ExpressChain Chain => chain;

        public void SaveChain(string fileName)
        {
            var serializer = new JsonSerializer();
            using (var stream = fileSystem.File.Open(fileName, System.IO.FileMode.Create, System.IO.FileAccess.Write))
            using (var writer = new JsonTextWriter(new System.IO.StreamWriter(stream)) { Formatting = Formatting.Indented })
            {
                serializer.Serialize(writer, chain);
            }
        }
    }
}
