using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract.Framework.Services;

[ManifestExtra("Author", "GeminiExpert")]
public class StorageTest : SmartContract
{
    private static readonly StorageMap Data = new StorageMap(Storage.CurrentContext, 0x01);

    public static bool Put(string key, string value)
    {
        Data.Put(key, value);
        Runtime.Log("Dato guardado correctamente");
        return true;
    }

    public static string Get(string key) => Data.Get(key);
}