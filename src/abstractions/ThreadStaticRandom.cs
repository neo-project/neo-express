using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Threading;

// https://gist.github.com/jaykang920/8234457
// https://devblogs.microsoft.com/pfxteam/getting-random-numbers-in-a-thread-safe-way/

public static class ThreadStaticRandom
{
    private static RNGCryptoServiceProvider global = new RNGCryptoServiceProvider();

    [ThreadStatic]
    private static Random? local;

    private static Random Local
    {
        get
        {
            if (local == null)
            {
                Span<byte> buffer = stackalloc byte[4];
                global.GetBytes(buffer);
                var seed = BinaryPrimitives.ReadInt32LittleEndian(buffer);
                var random = new Random(seed);
                Interlocked.CompareExchange(ref local, random, null);
            }
            return local;
        }
    }

    public static int Next() => Local.Next();

    public static int Next(int maxValue) => Local.Next(maxValue);

    public static int Next(int minValue, int maxValue) => Local.Next(minValue, maxValue);

    public static double NextDouble() => Local.NextDouble();

    public static void NextBytes(Span<byte> buffer) => Local.NextBytes(buffer);
}
