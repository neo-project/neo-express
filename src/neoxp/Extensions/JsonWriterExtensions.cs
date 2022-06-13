using System;
using Neo.BlockchainToolkit.Models;
using Newtonsoft.Json;
using Nito.Disposables;

namespace NeoExpress
{
    static class JsonWriterExtensions
    {
        public static IDisposable WriteObject(this JsonWriter writer)
        {
            writer.WriteStartObject();
            return AnonymousDisposable.Create(() => writer.WriteEndObject());
        }

        public static IDisposable WritePropertyObject(this JsonWriter writer, string property)
        {
            writer.WritePropertyName(property);
            return writer.WriteObject();
        }

        public static IDisposable WriteArray(this JsonWriter writer)
        {
            writer.WriteStartArray();
            return AnonymousDisposable.Create(() => writer.WriteEndArray());
        }

        public static IDisposable WritePropertyArray(this JsonWriter writer, string property)
        {
            writer.WritePropertyName(property);
            return writer.WriteArray();
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

        public static void WriteProperty(this JsonWriter writer, string property, long value)
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

        public static void WriteProperty(this JsonWriter writer, string property, ulong value)
        {
            writer.WritePropertyName(property);
            writer.WriteValue(value);
        }

        public static void WriteProperty(this JsonWriter writer, string property, bool value)
        {
            writer.WritePropertyName(property);
            writer.WriteValue(value);
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
                    writer.WriteStartArray();
                    using (var _ = AnonymousDisposable.Create(() => writer.WriteEndArray()))
                    {
                        foreach (var value in @array)
                        {
                            WriteJson(writer, value);
                        }
                    }
                    break;
                case Neo.IO.Json.JObject @object:
                    writer.WriteStartObject();
                    using (var _ = AnonymousDisposable.Create(() => writer.WriteEndObject()))
                    {
                        foreach (var (key, value) in @object.Properties)
                        {
                            writer.WritePropertyName(key);
                            WriteJson(writer, value);
                        }
                    }
                    break;
            }
        }
    }
}
