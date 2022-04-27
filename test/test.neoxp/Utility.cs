using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoExpressTest;

static class Utility
{
    public static Stream GetResourceStream(string name)
    {
        var assembly = typeof(Utility).Assembly;
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException();
        return assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException();
    }

    public static JToken GetResourceJson(string name)
    {
        using var resource = GetResourceStream(name);
        using var streamReader = new System.IO.StreamReader(resource);
        using var jsonReader = new JsonTextReader(streamReader);
        return JToken.ReadFrom(jsonReader);
    }

    public static string GetResource(string name)
    {
        using var resource = GetResourceStream(name);
        using var streamReader = new System.IO.StreamReader(resource);
        return streamReader.ReadToEnd();
    }

    public static ExpressChain GetResourceChain(string name)
    {
        var serializer = new JsonSerializer();
        using var stream = GetResourceStream(name);
        using var streamReader = new System.IO.StreamReader(stream);
        using var reader = new JsonTextReader(streamReader);
        return serializer.Deserialize<ExpressChain>(reader)
            ?? throw new Exception($"Cannot load ExpressChain from {name}");
    }

    public static ExpressChain LoadChain(this MockFileSystem fileSystem, string path)
    {
        var file = fileSystem.GetFile(path);
        return JsonConvert.DeserializeObject<ExpressChain>(file.TextContents);
    }


}
