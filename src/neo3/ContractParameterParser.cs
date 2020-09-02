using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Neo;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoExpress.Neo3
{
    // https://gist.github.com/devhawk/4394bb9be2af0b0ff54211b41574b65b
    public static class ContractParameterParser
    {
        public static async Task<Script> LoadInvocationScript(string invocationFilePath)
        {
            JToken invokeFileJson;
            {
                using var fileStream = File.OpenRead(invocationFilePath);
                using var textReader = new StreamReader(fileStream);
                using var jsonReader = new JsonTextReader(textReader);
                invokeFileJson = await JToken.LoadAsync(jsonReader).ConfigureAwait(false);
            }

            using var sb = new ScriptBuilder();
            switch (invokeFileJson.Type)
            {
                case JTokenType.Object:
                    EmitAppCall(sb, (JObject)invokeFileJson);
                    break;
                case JTokenType.Array:
                    {
                        JArray array = (JArray)invokeFileJson;
                        for (int i = 0; i < array.Count; i++)
                        {
                            if (array[i].Type != JTokenType.Object) 
                            {
                                throw new InvalidDataException("invalid invocation file");
                            }
                            EmitAppCall(sb, (JObject)array[i]);
                        }
                    }
                    break;
                default:
                    throw new InvalidDataException("invalid invocation file");
            }
            return sb.ToArray();

            void EmitAppCall(ScriptBuilder scriptBuilder, JObject json)
            {
                var contract = json.Value<string>("contract");
                contract = contract[0] == '#' ? contract[1..] : contract;
                if (!TryParseScriptHash(contract, invocationFilePath, out var scriptHash))
                {
                    throw new InvalidDataException();
                }
                var operation = json.Value<string>("operation");
                var args = ParseParams(json.GetValue("args"), invocationFilePath).ToArray();
                scriptBuilder.EmitAppCall(scriptHash, operation, args);
            }
        }

        private static string ResolvePath(string path, string root)
        {
            if (Path.IsPathFullyQualified(path))
            {
                return path;
            }

            if (root.Length == 0)
            {
                throw new InvalidOperationException("root path must be specified when using relative paths");
            }

            string rootDir = (File.GetAttributes(root) & FileAttributes.Directory) != 0
                ? root : Path.GetDirectoryName(root)!;
            return Path.Combine(rootDir, path);
        }

        private static bool TryParseScriptHash(string text, string root, [MaybeNullWhen(false)] out UInt160 value)
        {
            if (text.EndsWith(".nef"))
            {
                var path = ResolvePath(text, root);
                if (File.Exists(path))
                {
                    using var stream = File.OpenRead(path);
                    using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                    value = reader.ReadSerializable<NefFile>().ScriptHash;
                    return true;
                }
            }

            if (UInt160.TryParse(text, out value))
            {
                return true;
            }

            return false;
        }

        public static IEnumerable<ContractParameter> ParseParams(JToken? @params, string root = "")
        {
            if (@params == null)
            {
                return Enumerable.Empty<ContractParameter>();
            }

            if (@params is JArray jArray)
            {
                return jArray.Select(p => ParseParam(p, root));
            }

            return Enumerable.Repeat(ParseParam(@params, root), 1);
        }

        private static ContractParameter ParseParam(JToken param, string root)
        {
            return param.Type switch
            {
                JTokenType.String => ParseStringParam(param.Value<string>(), root),
                JTokenType.Boolean => new ContractParameter()
                {
                    Type = ContractParameterType.Boolean,
                    Value = param.Value<bool>()
                },
                JTokenType.Integer => new ContractParameter()
                {
                    Type = ContractParameterType.Integer,
                    Value = new BigInteger(param.Value<int>())
                },
                JTokenType.Array => new ContractParameter()
                {
                    Type = ContractParameterType.Array,
                    Value = ((JArray)param).Select(p => ParseParam(p, root)).ToList(),
                },
                JTokenType.Object => ParseObjectParam((JObject)param, root),
                _ => throw new ArgumentException(nameof(param))
            };
        }

        public static ContractParameter ParseStringParam(string param, string root = "")
        {
            if (TryParseAddress(out var scriptHash))
            {
                return new ContractParameter()
                {
                    Type = ContractParameterType.Hash160,
                    Value = scriptHash
                };
            }

            if (param[0] == '#'
                && TryParseScriptHash(param[1..], root, out var uint160))
            {
                return new ContractParameter()
                {
                    Type = ContractParameterType.Hash160,
                    Value = uint160
                };
            }

            if (param[0] == '#'
                && UInt256.TryParse(param[1..], out var uint256))
            {
                return new ContractParameter()
                {
                    Type = ContractParameterType.Hash256,
                    Value = uint256
                };
            }

            if (param.StartsWith("0x")
                && BigInteger.TryParse(param.AsSpan()[2..], NumberStyles.HexNumber, null, out var bigInteger))
            {
                return new ContractParameter()
                {
                    Type = ContractParameterType.Integer,
                    Value = bigInteger
                };
            }

            return new ContractParameter()
            {
                Type = ContractParameterType.String,
                Value = param
            };

            bool TryParseAddress(out UInt160 value)
            {
                try
                {
                    if (param[0] == '@')
                    {
                        value = param[1..].ToScriptHash();
                        return true;
                    }
                }
                catch
                {
                }

                value = default!;
                return false;
            }
        }

        private static ContractParameter ParseObjectParam(JObject param, string root = "")
        {
            var type = Enum.Parse<ContractParameterType>(param.Value<string>("type"));
            var jValue = param["value"] ?? throw new InvalidOperationException();

            object value = type switch
            {
                ContractParameterType.Array => jValue.Select(p => ParseParam(p, root)).ToArray(),
                ContractParameterType.Boolean => jValue.Value<bool>(),
                ContractParameterType.ByteArray => ParseByteArray(jValue),
                ContractParameterType.Hash160 => UInt160.Parse(jValue.Value<string>()),
                ContractParameterType.Hash256 => UInt256.Parse(jValue.Value<string>()),
                ContractParameterType.Integer => BigInteger.Parse(jValue.Value<string>()),
                ContractParameterType.Map => jValue.Select(e => ParseMapElement(e, root)).ToArray(),
                ContractParameterType.PublicKey => ECPoint.Parse(jValue.Value<string>(), ECCurve.Secp256r1),
                ContractParameterType.Signature => ParseByteArray(jValue),
                ContractParameterType.String => jValue.Value<string>(),
                _ => throw new ArgumentException(nameof(param) + $" {type}"),
            };

            return new ContractParameter()
            {
                Type = type,
                Value = value,
            };

            static byte[] ParseByteArray(JToken json)
            {
                var value = json.Value<string>();
                return value.StartsWith("0x")
                    ? value[2..].HexToBytes()
                    : Convert.FromBase64String(value);
            }

            static KeyValuePair<ContractParameter, ContractParameter> ParseMapElement(JToken json, string root) =>
                KeyValuePair.Create(
                    ParseParam(json["key"] ?? throw new InvalidOperationException(), root),
                    ParseParam(json["value"] ?? throw new InvalidOperationException(), root));
        }
    }
}