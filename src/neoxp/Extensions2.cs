using System;
using System.IO.Abstractions;
using NeoExpress.Models;
using Newtonsoft.Json;

namespace NeoExpress
{
    static class Extensions2
    {
        public static string GetDefaultFilename(this IFileSystem @this, string filename) => string.IsNullOrEmpty(filename)
           ? @this.Path.Combine(@this.Directory.GetCurrentDirectory(), "default.neo-express")
           : filename;

        public static ExpressChain LoadChain(this IFileSystem @this, string filename)
        {
            var serializer = new JsonSerializer();
            using var stream = @this.File.OpenRead(filename);
            using var reader = new JsonTextReader(new System.IO.StreamReader(stream));
            return serializer.Deserialize<ExpressChain>(reader)
                ?? throw new Exception($"Cannot load Neo-Express instance information from {filename}");
        }

        public static void SaveChain(this IFileSystem @this, ExpressChain chain, string fileName)
        {
            var serializer = new JsonSerializer();
            using (var stream = @this.File.Open(fileName, System.IO.FileMode.Create, System.IO.FileAccess.Write))
            using (var writer = new JsonTextWriter(new System.IO.StreamWriter(stream)) { Formatting = Formatting.Indented })
            {
                serializer.Serialize(writer, chain);
            }
        }
    }
}
