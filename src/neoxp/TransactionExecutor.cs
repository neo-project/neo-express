using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Neo;
using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using NeoExpress.Models;
using Newtonsoft.Json.Linq;
using OneOf;
using OneOf.Types;

namespace NeoExpress
{
    class TransactionExecutor : IDisposable
    {
        readonly IExpressNode expressNode = null!;
        readonly IFileSystem fileSystem;
        readonly bool json;
        readonly System.IO.TextWriter writer;

        public TransactionExecutor(IFileSystem fileSystem, ExpressChain chain, bool trace, bool json, TextWriter writer)
        {
            // expressNode = chain.GetExpressNode(fileSystem, trace);
            this.fileSystem = fileSystem;
            this.json = json;
            this.writer = writer;
        }

        public void Dispose()
        {
            expressNode.Dispose();
        }

        public IExpressNode ExpressNode => expressNode;

        public async Task<Script> LoadInvocationScriptAsync(string invocationFile)
        {
            if (!fileSystem.File.Exists(invocationFile))
            {
                throw new Exception($"Invocation file {invocationFile} couldn't be found");
            }

            var parser = await expressNode.GetContractParameterParserAsync().ConfigureAwait(false);
            return await parser.LoadInvocationScriptAsync(invocationFile).ConfigureAwait(false);
        }

        public async Task<Script> BuildInvocationScriptAsync(string contract, string operation, IReadOnlyList<string>? arguments = null)
        {
            if (string.IsNullOrEmpty(operation))
                throw new InvalidOperationException($"invalid contract operation \"{operation}\"");

            var parser = await expressNode.GetContractParameterParserAsync().ConfigureAwait(false);
            var scriptHash = parser.TryLoadScriptHash(contract, out var value)
                ? value
                : UInt160.TryParse(contract, out var uint160)
                    ? uint160
                    : throw new InvalidOperationException($"contract \"{contract}\" not found");

            arguments ??= Array.Empty<string>();
            var @params = new ContractParameter[arguments.Count];
            for (int i = 0; i < arguments.Count; i++)
            {
                @params[i] = ConvertArg(arguments[i], parser);
            }

            using var scriptBuilder = new ScriptBuilder();
            scriptBuilder.EmitDynamicCall(scriptHash, operation, @params);
            return scriptBuilder.ToArray();

            static ContractParameter ConvertArg(string arg, ContractParameterParser parser)
            {
                if (bool.TryParse(arg, out var boolArg))
                {
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Boolean,
                        Value = boolArg
                    };
                }

                if (long.TryParse(arg, out var longArg))
                {
                    return new ContractParameter()
                    {
                        Type = ContractParameterType.Integer,
                        Value = new BigInteger(longArg)
                    };
                }

                return parser.ParseParameter(arg);
            }
        }

        public async Task ContractInvokeAsync(Script script, string accountName, string password, WitnessScope witnessScope, decimal additionalGas = 0m)
        {
            await Task.FromException(new NotImplementedException());
            // if (!expressNode.Chain.TryGetSigningAccount(accountName, password, fileSystem, out var wallet, out var accountHash))
            // {
            //     throw new Exception($"{accountName} account not found.");
            // }

            // var txHash = await expressNode.ExecuteAsync(wallet, accountHash, witnessScope, script, additionalGas).ConfigureAwait(false);
            // await writer.WriteTxHashAsync(txHash, "Invocation", json).ConfigureAwait(false);
        }

        public async Task InvokeForResultsAsync(Script script, string accountName, WitnessScope witnessScope)
        {
            await Task.FromException(new NotImplementedException());

            // Signer? signer = expressNode.Chain.TryGetSigningAccount(accountName, string.Empty, fileSystem, out _, out var accountHash)
            //     ? signer = new Signer
            //     {
            //         Account = accountHash,
            //         Scopes = witnessScope,
            //         AllowedContracts = Array.Empty<UInt160>(),
            //         AllowedGroups = Array.Empty<Neo.Cryptography.ECC.ECPoint>()
            //     }
            //     : null;

            // var result = await expressNode.InvokeAsync(script, signer).ConfigureAwait(false);
            // if (json)
            // {
            //     await writer.WriteLineAsync(result.ToJson().ToString(true)).ConfigureAwait(false);
            // }
            // else
            // {
            //     await writer.WriteLineAsync($"VM State:     {result.State}").ConfigureAwait(false);
            //     await writer.WriteLineAsync($"Gas Consumed: {result.GasConsumed}").ConfigureAwait(false);
            //     if (!string.IsNullOrEmpty(result.Exception))
            //     {
            //         await writer.WriteLineAsync($"Exception:   {result.Exception}").ConfigureAwait(false);
            //     }
            //     if (result.Stack.Length > 0)
            //     {
            //         var stack = result.Stack;
            //         await writer.WriteLineAsync("Result Stack:").ConfigureAwait(false);
            //         for (int i = 0; i < stack.Length; i++)
            //         {
            //             await WriteStackItemAsync(writer, stack[i]).ConfigureAwait(false);
            //         }
            //     }
            // }

            // static async Task WriteStackItemAsync(System.IO.TextWriter writer, Neo.VM.Types.StackItem item, int indent = 1, string prefix = "")
            // {
            //     switch (item)
            //     {
            //         case Neo.VM.Types.Boolean _:
            //             await WriteLineAsync(item.GetBoolean() ? "true" : "false").ConfigureAwait(false);
            //             break;
            //         case Neo.VM.Types.Integer @int:
            //             await WriteLineAsync(@int.GetInteger().ToString()).ConfigureAwait(false);
            //             break;
            //         case Neo.VM.Types.Buffer buffer:
            //             await WriteLineAsync(Neo.Helper.ToHexString(buffer.GetSpan())).ConfigureAwait(false);
            //             break;
            //         case Neo.VM.Types.ByteString byteString:
            //             await WriteLineAsync(Neo.Helper.ToHexString(byteString.GetSpan())).ConfigureAwait(false);
            //             break;
            //         case Neo.VM.Types.Null _:
            //             await WriteLineAsync("<null>").ConfigureAwait(false);
            //             break;
            //         case Neo.VM.Types.Array array:
            //             await WriteLineAsync($"Array: ({array.Count})").ConfigureAwait(false);
            //             for (int i = 0; i < array.Count; i++)
            //             {
            //                 await WriteStackItemAsync(writer, array[i], indent + 1).ConfigureAwait(false);
            //             }
            //             break;
            //         case Neo.VM.Types.Map map:
            //             await WriteLineAsync($"Map: ({map.Count})").ConfigureAwait(false);
            //             foreach (var m in map)
            //             {
            //                 await WriteStackItemAsync(writer, m.Key, indent + 1, "key:   ").ConfigureAwait(false);
            //                 await WriteStackItemAsync(writer, m.Value, indent + 1, "value: ").ConfigureAwait(false);
            //             }
            //             break;
            //     }

            //     async Task WriteLineAsync(string value)
            //     {
            //         for (var i = 0; i < indent; i++)
            //         {
            //             await writer.WriteAsync("  ").ConfigureAwait(false);
            //         }

            //         if (!string.IsNullOrEmpty(prefix))
            //         {
            //             await writer.WriteAsync(prefix).ConfigureAwait(false);
            //         }

            //         await writer.WriteLineAsync(value).ConfigureAwait(false);
            //     }
            // }
        }

        public static bool TryParseRpcUri(string value, [NotNullWhen(true)] out Uri? uri)
        {
            if (value.Equals("mainnet", StringComparison.OrdinalIgnoreCase))
            {
                uri = new Uri("http://seed1.neo.org:10332");
                return true;
            }

            if (value.Equals("testnet", StringComparison.OrdinalIgnoreCase))
            {
                uri = new Uri("http://seed1t4.neo.org:20332");
                return true;
            }

            return (Uri.TryCreate(value, UriKind.Absolute, out uri)
                && uri != null
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));
        }
    }
}
