// Copyright (C) 2015-2026 The Neo Project.
//
// ContractParameterParser.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.IO;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Numerics;

namespace Neo.BlockchainToolkit
{
    public class ContractParameterParser
    {
        internal const long MaxInvocationFileBytes = 4 * 1024 * 1024;

        public delegate bool TryGetUInt160(string value, [MaybeNullWhen(false)] out UInt160 account);

        private readonly byte addressVersion;
        private readonly TryGetUInt160? tryGetAccount;
        private readonly TryGetUInt160? tryGetContract;
        private readonly IFileSystem fileSystem;

        public byte AddressVersion => addressVersion;

        public ContractParameterParser(ProtocolSettings protocolSettings, TryGetUInt160? tryGetAccount = null, TryGetUInt160? tryGetContract = null, IFileSystem? fileSystem = null)
            : this(protocolSettings.AddressVersion, tryGetAccount, tryGetContract, fileSystem)
        {
        }

        public ContractParameterParser(byte addressVersion, TryGetUInt160? tryGetAccount = null, TryGetUInt160? tryGetContract = null, IFileSystem? fileSystem = null)
        {
            this.addressVersion = addressVersion;
            this.tryGetAccount = tryGetAccount;
            this.tryGetContract = tryGetContract;
            this.fileSystem = fileSystem ?? new FileSystem();
        }

        public async Task<Script> LoadInvocationScriptAsync(string path)
        {
            var invokeFile = fileSystem.Path.GetFullPath(path);
            var fileInfo = fileSystem.FileInfo.New(invokeFile);
            if (!fileInfo.Exists)
                throw new ArgumentException($"{path} doesn't exist", nameof(path));
            if ((fileInfo.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                throw new ArgumentException($"{path} is not a file", nameof(path));
            if (fileInfo.Length > MaxInvocationFileBytes)
                throw new Exception($"Invocation file {path} is invalid: file is larger than {MaxInvocationFileBytes} bytes");

            using var stream = fileSystem.File.OpenRead(invokeFile);
            using var budgetedStream = new ReadBudgetStream(stream, MaxInvocationFileBytes);
            using var streamReader = new StreamReader(budgetedStream);
            using var jsonReader = new JsonTextReader(streamReader);
            try
            {
                var document = await JContainer.LoadAsync(jsonReader).ConfigureAwait(false);
                return LoadInvocationScript(document);
            }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidCastException or IOException)
            {
                throw new Exception($"Invocation file {path} is invalid: {ex.Message}");
            }
        }

        sealed class ReadBudgetStream : Stream
        {
            readonly Stream inner;
            readonly long maxBytes;
            long bytesRead;

            public ReadBudgetStream(Stream inner, long maxBytes)
            {
                this.inner = inner;
                this.maxBytes = maxBytes;
            }

            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => inner.CanSeek;
            public override bool CanWrite => false;
            public override long Length => inner.Length;
            public override long Position
            {
                get => inner.Position;
                set => inner.Position = value;
            }

            public override void Flush() => inner.Flush();

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = inner.Read(buffer, offset, GetAllowedCount(count));
                AddBytesRead(read);
                return read;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                var allowedCount = GetAllowedCount(buffer.Length);
                var read = await inner.ReadAsync(buffer[..allowedCount], cancellationToken).ConfigureAwait(false);
                AddBytesRead(read);
                return read;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var read = await inner.ReadAsync(buffer, offset, GetAllowedCount(count), cancellationToken).ConfigureAwait(false);
                AddBytesRead(read);
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            int GetAllowedCount(int requestedCount)
            {
                if (requestedCount == 0)
                    return 0;

                var remaining = maxBytes - bytesRead;
                if (remaining <= 0)
                    throw new IOException($"file is larger than {maxBytes} bytes");

                return (int)Math.Min(requestedCount, remaining + 1);
            }

            void AddBytesRead(int count)
            {
                if (count == 0)
                    return;

                bytesRead += count;
                if (bytesRead > maxBytes)
                    throw new IOException($"file is larger than {maxBytes} bytes");
            }
        }

        private Script LoadInvocationScript(JToken document)
        {
            var scriptBuilder = new ScriptBuilder();
            switch (document.Type)
            {
                case JTokenType.Object:
                    EmitAppCall((JObject)document);
                    break;
                case JTokenType.Array:
                    {
                        foreach (var item in document)
                        {
                            EmitAppCall((JObject)item);
                        }
                    }
                    break;
                default:
                    throw new FormatException("invalid invocation file");
            }
            return scriptBuilder.ToArray();

            void EmitAppCall(JObject json)
            {
                var contract = json.Value<string>("contract")
                    ?? throw new JsonException("missing contract field");
                contract = contract.Length > 0 && contract[0] == '#'
                    ? contract[1..] : contract;

                var scriptHash = TryLoadScriptHash(contract, out var value)
                    ? value
                    : UInt160.TryParse(contract, out var uint160)
                        ? uint160
                        : throw new InvalidOperationException($"contract \"{contract}\" not found");

                var operation = json.Value<string>("operation")
                    ?? throw new JsonException("missing operation field");

                var args = json.TryGetValue("args", out var jsonArgs)
                    ? ParseParameters(jsonArgs).ToArray()
                    : Array.Empty<ContractParameter>();

                scriptBuilder.EmitDynamicCall(scriptHash, operation, args);
            }
        }

        public IEnumerable<ContractParameter> ParseParameters(JToken json)
            => json.Type switch
            {
                JTokenType.Array => json.Select(e => ParseParameter(e)),
                _ => new[] { ParseParameter(json) }
            };

        public static ContractParameter ConvertStackItem(Neo.VM.Types.StackItem item)
        {
            return item switch
            {
                // Neo.VM.Types.Struct value handled by Array branch
                Neo.VM.Types.Array value => new ContractParameter()
                {
                    Type = ContractParameterType.Array,
                    Value = value.Select(ConvertStackItem).ToList()
                },
                Neo.VM.Types.Boolean value => new ContractParameter()
                {
                    Type = ContractParameterType.Boolean,
                    Value = value.GetBoolean()
                },
                Neo.VM.Types.Buffer value => new ContractParameter()
                {
                    Type = ContractParameterType.ByteArray,
                    Value = value.InnerBuffer
                },
                Neo.VM.Types.ByteString value => new ContractParameter()
                {
                    Type = ContractParameterType.ByteArray,
                    Value = value.GetSpan().ToArray()
                },
                Neo.VM.Types.Integer value => new ContractParameter()
                {
                    Type = ContractParameterType.Integer,
                    Value = value.GetInteger()
                },
                Neo.VM.Types.Map value => new ContractParameter()
                {
                    Type = ContractParameterType.Map,
                    Value = value.Select(kvp => KeyValuePair.Create(ConvertStackItem(kvp.Key), ConvertStackItem(kvp.Value))).ToList()
                },
                Neo.VM.Types.Null value => new ContractParameter
                {
                    Type = ContractParameterType.Any,
                    Value = null
                },
                Neo.VM.Types.InteropInterface _ => throw new NotSupportedException("InteropInterface instances cannot be converted into a ContractParameter"),
                Neo.VM.Types.Pointer _ => throw new NotSupportedException("Pointer instances cannot be converted into a ContractParameter"),
                _ => throw new ArgumentException($"Unknown Stack Item Type {item.GetType().Name}", nameof(item)),
            };
        }

        // logic for ConvertObject borrowed from Neo.VM.Helper.EmitPush(ScriptBuilder, object)
        // but extended to support converting more types to ContractParameter (StackItem types in particular)
        public static ContractParameter ConvertObject(object? obj)
        {
            return obj switch
            {
                ContractParameter value => value,
                Neo.VM.Types.StackItem value => ConvertStackItem(value),
                bool value => new ContractParameter()
                {
                    Type = ContractParameterType.Boolean,
                    Value = value
                },
                byte[] value => new ContractParameter()
                {
                    Type = ContractParameterType.ByteArray,
                    Value = value,
                },
                string value => new ContractParameter
                {
                    Type = ContractParameterType.String,
                    Value = value,
                },
                BigInteger value => new ContractParameter
                {
                    Type = ContractParameterType.Integer,
                    Value = value,
                },
                sbyte value => new ContractParameter
                {
                    Type = ContractParameterType.Integer,
                    Value = new BigInteger(value),
                },
                byte value => new ContractParameter
                {
                    Type = ContractParameterType.Integer,
                    Value = new BigInteger(value),
                },
                short value => new ContractParameter
                {
                    Type = ContractParameterType.Integer,
                    Value = new BigInteger(value),
                },
                ushort value => new ContractParameter
                {
                    Type = ContractParameterType.Integer,
                    Value = new BigInteger(value),
                },
                int value => new ContractParameter
                {
                    Type = ContractParameterType.Integer,
                    Value = new BigInteger(value),
                },
                uint value => new ContractParameter
                {
                    Type = ContractParameterType.Integer,
                    Value = new BigInteger(value),
                },
                long value => new ContractParameter
                {
                    Type = ContractParameterType.Integer,
                    Value = new BigInteger(value),
                },
                ulong value => new ContractParameter
                {
                    Type = ContractParameterType.Integer,
                    Value = new BigInteger(value),
                },
                Enum value => new ContractParameter
                {
                    Type = ContractParameterType.Integer,
                    Value = BigInteger.Parse(value.ToString("d")),
                },
                ISerializable value => new ContractParameter
                {
                    Type = ContractParameterType.ByteArray,
                    Value = value.ToArray(),
                },
                null => new ContractParameter
                {
                    Type = ContractParameterType.Any,
                    Value = null
                },
                _ => throw new ArgumentException($"Unconvertible parameter type {obj.GetType().Name}", nameof(obj)),
            };
        }

        public ContractParameter ParseParameter(JToken? json)
        {
            if (json == null)
            {
                return new ContractParameter() { Type = ContractParameterType.Any };
            }

            return json.Type switch
            {
                JTokenType.Null => new ContractParameter() { Type = ContractParameterType.Any },
                JTokenType.Boolean => new ContractParameter()
                {
                    Type = ContractParameterType.Boolean,
                    Value = json.Value<bool>(),
                },
                JTokenType.Integer => new ContractParameter()
                {
                    Type = ContractParameterType.Integer,
                    Value = new BigInteger(json.Value<long>())
                },
                JTokenType.Array => new ContractParameter()
                {
                    Type = ContractParameterType.Array,
                    Value = ((JArray)json)
                        .Select(e => ParseParameter(e))
                        .ToList()
                },
                JTokenType.String => ParseStringParameter(json.Value<string>() ?? ""),
                JTokenType.Object => ParseObjectParameter((JObject)json),
                _ => throw new ArgumentException($"Invalid JTokenType {json.Type}", nameof(json))
            };
        }

        public ContractParameter ParseStringParameter(string value)
        {
            if (value.Length >= 1)
            {
                if (value[0] == '@')
                {
                    var substring = value[1..];

                    if (tryGetAccount != null && tryGetAccount(substring, out var account))
                    {
                        return new ContractParameter(ContractParameterType.Hash160) { Value = account };
                    }

                    if (TryParseAddress(substring, addressVersion, out var address))
                    {
                        return new ContractParameter(ContractParameterType.Hash160) { Value = address };
                    }
                }
                else if (value[0] == '#')
                {
                    var substring = value[1..];

                    if (UInt160.TryParse(substring, out var uint160))
                    {
                        return new ContractParameter(ContractParameterType.Hash160) { Value = uint160 };
                    }

                    if (UInt256.TryParse(substring, out var uint256))
                    {
                        return new ContractParameter(ContractParameterType.Hash256) { Value = uint256 };
                    }

                    if (TryLoadScriptHash(substring, out var scriptHash))
                    {
                        return new ContractParameter(ContractParameterType.Hash160) { Value = scriptHash };
                    }
                }
            }

            if (value.StartsWith("file://"))
            {
                var file = fileSystem.NormalizePath(value[7..]);
                file = fileSystem.Path.IsPathFullyQualified(file)
                    ? file
                    : fileSystem.Path.GetFullPath(file, fileSystem.Directory.GetCurrentDirectory());

                if (!fileSystem.File.Exists(file))
                {
                    throw new System.IO.FileNotFoundException(null, file);
                }

                return new ContractParameter(ContractParameterType.ByteArray)
                {
                    Value = fileSystem.File.ReadAllBytes(file)
                };
            }

            if (TryParseHexString(value, out var byteArray))
            {
                return new ContractParameter(ContractParameterType.ByteArray) { Value = byteArray };
            }

            return new ContractParameter(ContractParameterType.String) { Value = value };

            static bool TryParseAddress(string address, byte version, [MaybeNullWhen(false)] out UInt160 scriptHash)
            {
                try
                {
                    scriptHash = address.ToScriptHash(version);
                    return true;
                }
                catch (FormatException) { }

                scriptHash = default!;
                return false;
            }
        }

        static bool TryParseHexString(string hexString, [MaybeNullWhen(false)] out byte[] array)
        {
            if (hexString.StartsWith("0x"))
            {
                try
                {
                    array = Convert.FromHexString(hexString.AsSpan()[2..]);
                    return true;
                }
                catch (FormatException) { }
            }

            array = default!;
            return false;
        }

        public bool TryLoadScriptHash(string text, [MaybeNullWhen(false)] out UInt160 value)
        {
            if (tryGetContract != null && tryGetContract(text, out var scriptHash))
            {
                value = scriptHash;
                return true;
            }

            var nativeContract = NativeContract.Contracts.SingleOrDefault(c => string.Equals(text, c.Name));
            if (nativeContract != null)
            {
                value = nativeContract.Hash;
                return true;
            }

            nativeContract = NativeContract.Contracts.SingleOrDefault(c => string.Equals(text, c.Name, StringComparison.OrdinalIgnoreCase));
            if (nativeContract != null)
            {
                value = nativeContract.Hash;
                return true;
            }

            value = null!;
            return false;
        }

        internal ContractParameter ParseObjectParameter(JObject json)
        {
            var type = Enum.Parse<ContractParameterType>(json.Value<string>("type") ?? throw new JsonException("missing type field"));
            var valueProp = json["value"] ?? throw new JsonException("missing value field");

            object value = type switch
            {
                ContractParameterType.Signature or ContractParameterType.ByteArray => ParseBinary(valueProp),
                ContractParameterType.Boolean => valueProp.Value<bool>(),
                ContractParameterType.Integer => BigInteger.Parse(valueProp.Value<string>() ?? ""),
                ContractParameterType.Hash160 => UInt160.Parse(valueProp.Value<string>()),
                ContractParameterType.Hash256 => UInt256.Parse(valueProp.Value<string>()),
                ContractParameterType.PublicKey => ECPoint.Parse(valueProp.Value<string>(), ECCurve.Secp256r1),
                ContractParameterType.String => valueProp.Value<string>() ?? throw new JsonException(),
                ContractParameterType.Array => valueProp.Select(ParseParameter).ToList(),
                ContractParameterType.Map => valueProp.Select(ParseMapElement).ToList(),
                _ => throw new ArgumentException($"invalid type {type}", nameof(json)),
            };

            return new ContractParameter() { Type = type, Value = value };

            KeyValuePair<ContractParameter, ContractParameter> ParseMapElement(JToken json)
                => KeyValuePair.Create(
                    ParseParameter(json["key"] ?? throw new JsonException("missing key property")),
                    ParseParameter(json["value"] ?? throw new JsonException("missing value property")));

            static byte[] ParseBinary(JToken json)
            {
                var value = json.Value<string>() ?? "";
                Span<byte> span = stackalloc byte[value.Length / 4 * 3];
                if (Convert.TryFromBase64String(value, span, out var written))
                {
                    return span.Slice(0, written).ToArray();
                }

                if (TryParseHexString(value, out var byteArray))
                {
                    return byteArray;
                }

                throw new ArgumentException($"ContractParameterParser could not parse \"{value}\" as binary", nameof(json));
            }
        }
    }
}
