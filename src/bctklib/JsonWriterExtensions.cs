// Copyright (C) 2015-2024 The Neo Project.
//
// JsonWriterExtensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.BlockchainToolkit
{
    public static class JsonWriterExtensions
    {
        public static IDisposable WriteArray(this JsonWriter writer)
        {
            writer.WriteStartArray();
            return new DelegateDisposable(writer.WriteEndArray);
        }

        public static IDisposable WriteObject(this JsonWriter writer)
        {
            writer.WriteStartObject();
            return new DelegateDisposable(writer.WriteEndObject);
        }

        public static IDisposable WritePropertyObject(this JsonWriter writer, string property)
        {
            writer.WritePropertyName(property);
            writer.WriteStartObject();
            return new DelegateDisposable(writer.WriteEndObject);
        }

        public static IDisposable WritePropertyArray(this JsonWriter writer, string property)
        {
            writer.WritePropertyName(property);
            writer.WriteStartArray();
            return new DelegateDisposable(writer.WriteEndArray);
        }

        public static void WritePropertyNull(this JsonWriter writer, string property)
        {
            writer.WritePropertyName(property);
            writer.WriteNull();
        }

        public static void WritePropertyBase64(this JsonWriter writer, string property, ReadOnlySpan<byte> data)
        {
            writer.WritePropertyName(property);
            writer.WriteValue(Convert.ToBase64String(data));
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

        public static void WriteJson(this JsonWriter writer, Neo.Json.JToken? json)
        {
            switch (json)
            {
                case null:
                    writer.WriteNull();
                    break;
                case Neo.Json.JBoolean boolean:
                    writer.WriteValue(boolean.Value);
                    break;
                case Neo.Json.JNumber number:
                    writer.WriteValue(number.Value);
                    break;
                case Neo.Json.JString @string:
                    writer.WriteValue(@string.Value);
                    break;
                case Neo.Json.JArray @array:
                    using (var _ = writer.WriteArray())
                    {
                        foreach (var value in @array)
                        {
                            WriteJson(writer, value);
                        }
                    }
                    break;
                case Neo.Json.JObject @object:
                    using (var _ = writer.WriteObject())
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

        public static Neo.Json.JToken? ToNeoJson(this JToken? json)
        {
            if (json is null)
                return null;

            return json.Type switch
            {
                JTokenType.Null => null,
                JTokenType.Boolean => json.Value<bool>(),
                JTokenType.Float => json.Value<double>(),
                JTokenType.Integer => json.Value<long>(),
                JTokenType.String => json.Value<string>(),
                JTokenType.Array => new Neo.Json.JArray(json.Select(ToNeoJson)),
                JTokenType.Object => ConvertJObject((JObject)json),
                _ => throw new NotSupportedException($"{json.Type}"),
            };
            static Neo.Json.JObject ConvertJObject(JObject json)
            {
                var neoJson = new Neo.Json.JObject();
                foreach (var (key, value) in json)
                {
                    neoJson[key] = ToNeoJson(value);
                }
                return neoJson;
            }
        }
    }
}
