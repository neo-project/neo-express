using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Neo;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Neo.Wallets;
using Newtonsoft.Json.Linq;

namespace NeoExpress.Neo3
{
    // https://gist.github.com/devhawk/4394bb9be2af0b0ff54211b41574b65b
    public static class ContractParameterParser
    {
        public static IEnumerable<ContractParameter> ParseParams(JToken? @params)
        {
            if (@params == null)
            {
                return Enumerable.Empty<ContractParameter>();
            }

            if (@params is JArray jArray)
            {
                return jArray.Select(ParseParam);
            }

            return Enumerable.Repeat(ParseParam(@params), 1);
        }

        private static ContractParameter ParseParam(JToken param)
        {
            return param.Type switch
            {
                JTokenType.String => ParseStringParam(param.Value<string>()),
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
                    Value = ((JArray)param).Select(ParseParam).ToList(),
                },
                JTokenType.Object => ParseObjectParam((JObject)param),
                _ => throw new ArgumentException(nameof(param))
            };
        }

        private static ContractParameter ParseStringParam(string param)
        {
            if (param.StartsWith("@N"))
            {
                return new ContractParameter()
                {
                    Type = ContractParameterType.Hash160,
                    Value = param.Substring(1).ToScriptHash()
                };
            }

            if (param.StartsWith("0x")
                && BigInteger.TryParse(param.AsSpan().Slice(2), NumberStyles.HexNumber, null, out var bigInteger))
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
        }

        private static ContractParameter ParseObjectParam(JObject param)
        {
            var type = Enum.Parse<ContractParameterType>(param.Value<string>("type"));
            var jValue = param["value"] ?? throw new InvalidOperationException();

            object value = type switch
            {
                ContractParameterType.Array => jValue.Select(ParseParam).ToArray(),
                ContractParameterType.Boolean => jValue.Value<bool>(),
                ContractParameterType.ByteArray => ParseByteArray(jValue),
                ContractParameterType.Hash160 => UInt160.Parse(jValue.Value<string>()),
                ContractParameterType.Hash256 => UInt256.Parse(jValue.Value<string>()),
                ContractParameterType.Integer => BigInteger.Parse(jValue.Value<string>()),
                ContractParameterType.Map => jValue.Select(ParseMapElement).ToArray(),
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
                    ? value.Substring(2).HexToBytes()
                    : Convert.FromBase64String(value);
            }

            static KeyValuePair<ContractParameter, ContractParameter> ParseMapElement(JToken json) =>
                KeyValuePair.Create(
                    ParseParam(json["key"] ?? throw new InvalidOperationException()),
                    ParseParam(json["value"] ?? throw new InvalidOperationException()));
        }
    }
}
