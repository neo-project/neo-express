using System.IO.Abstractions;

namespace NeoExpress
{
    static class Extensions2
    {
        public static string GetDefaultFilename(this IFileSystem @this, string filename) => string.IsNullOrEmpty(filename)
           ? @this.Path.Combine(@this.Directory.GetCurrentDirectory(), "default.neo-express")
           : filename;

    }
}
