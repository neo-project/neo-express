using System.IO;
using NeoExpress.Neo2.Models;
using Newtonsoft.Json;

namespace NeoExpress
{
    static class Neo2Extensions
    {
        public static void Save(this ExpressChain chain, string fileName)
        {
            var serializer = new JsonSerializer();
            using (var stream = File.Open(fileName, FileMode.Create, FileAccess.Write))
            using (var writer = new JsonTextWriter(new StreamWriter(stream)) { Formatting = Formatting.Indented })
            {
                serializer.Serialize(writer, chain);
            }
        }
    }
}
