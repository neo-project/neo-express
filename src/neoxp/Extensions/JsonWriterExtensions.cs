using System;
using System.IO;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;
using Nito.Disposables;

namespace NeoExpress
{
    static class JsonWriterExtensions
    {
        public static IDisposable WriteStartArrayAuto(this JsonWriter writer)
        {
            writer.WriteStartArray();
            return AnonymousDisposable.Create(() => writer.WriteEndArray());
        }

        public static IDisposable WriteStartObjectAuto(this JsonWriter writer)
        {
            writer.WriteStartObject();
            return AnonymousDisposable.Create(() => writer.WriteEndObject());
        }

        public static void WritePropertyNull(this JsonWriter writer, string property)
        {
            writer.WritePropertyName(property);
            writer.WriteNull();
        }

        public static void WriteProperty(this JsonWriter writer, string property, string value)
        {
            writer.WritePropertyName(property);
            writer.WriteValue(value);
        }

        public static void WriteProperty(this JsonWriter writer, string property, byte value)
        {
            writer.WritePropertyName(property);
            writer.WriteValue(value);
        }

        public static void WriteProperty(this JsonWriter writer, string property, int value)
        {
            writer.WritePropertyName(property);
            writer.WriteValue(value);
        }

        public static void WriteProperty(this JsonWriter writer, string property, uint value)
        {
            writer.WritePropertyName(property);
            writer.WriteValue(value);
        }

        public static void WriteProperty(this JsonWriter writer, string property, ushort value)
        {
            writer.WritePropertyName(property);
            writer.WriteValue(value);
        }

        public static void WriteProperty(this JsonWriter writer, string property, bool value)
        {
            writer.WritePropertyName(property);
            writer.WriteValue(value);
        }
        public static void WriteWallet(this TextWriter writer, ExpressWallet wallet)
        {
            writer.WriteLine(wallet.Name);

            foreach (var account in wallet.Accounts)
            {
                writer.WriteAccount(account);
            }
        }

        public static void WriteJson(this JsonWriter writer, Neo.IO.Json.JObject json)
        {
            switch (json)
            {
                case null:
                    writer.WriteNull();
                    break;
                case Neo.IO.Json.JBoolean boolean:
                    writer.WriteValue(boolean.Value);
                    break;
                case Neo.IO.Json.JNumber number:
                    writer.WriteValue(number.Value);
                    break;
                case Neo.IO.Json.JString @string:
                    writer.WriteValue(@string.Value);
                    break;
                case Neo.IO.Json.JArray @array:
                    {
                        using var _ = writer.WriteStartArrayAuto();
                        foreach (var value in @array)
                        {
                            WriteJson(writer, value);
                        }
                        break;
                    }
                case Neo.IO.Json.JObject @object:
                    {
                        using var _ = writer.WriteStartObjectAuto();
                        foreach (var (key, value) in @object.Properties)
                        {
                            writer.WritePropertyName(key);
                            WriteJson(writer, value);
                        }
                        break;
                    }
            }
        }
    }
}
