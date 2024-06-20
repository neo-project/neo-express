// Copyright (C) 2015-2024 The Neo Project.
//
// ContractCommand.Storage.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using McMaster.Extensions.CommandLineUtils;
using Neo;
using Neo.BlockchainToolkit;
using Neo.SmartContract.Manifest;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Text;

namespace NeoExpress.Commands
{
    partial class ContractCommand
    {
        [Command(Name = "storage", Description = "Manage smart contracts storages")]
        [Subcommand(
        typeof(StorageGet),
        typeof(StorageUpdateKeyValue))]
        internal class Storage
        {
            [Command("get", Description = "Display storage for specified contract")]
            public class StorageGet
            {
                readonly ExpressChainManagerFactory chainManagerFactory;

                public StorageGet(ExpressChainManagerFactory chainManagerFactory)
                {
                    this.chainManagerFactory = chainManagerFactory;
                }

                [Argument(0, Description = "Contract name or invocation hash")]
                [Required]
                internal string Contract { get; init; } = string.Empty;

                [Option(Description = "Path to neo-express data file")]
                internal string Input { get; init; } = string.Empty;

                [Option(Description = "Output as JSON")]
                internal bool Json { get; }

                internal async Task WriteStoragesAsync(IExpressNode expressNode, TextWriter writer, IReadOnlyList<(UInt160 hash, ContractManifest)> contracts)
                {
                    if (Json)
                    {
                        using var jsonWriter = new JsonTextWriter(writer);

                        if (contracts.Count > 1)
                            await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);

                        for (int i = 0; i < contracts.Count; i++)
                        {
                            var storages = await expressNode.ListStoragesAsync(contracts[i].hash).ConfigureAwait(false);

                            await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);

                            await jsonWriter.WritePropertyNameAsync("script-hash").ConfigureAwait(false);
                            await jsonWriter.WriteValueAsync(contracts[i].hash.ToString()).ConfigureAwait(false);

                            await jsonWriter.WritePropertyNameAsync("storages").ConfigureAwait(false);
                            await jsonWriter.WriteStartArrayAsync().ConfigureAwait(false);
                            for (int j = 0; j < storages.Count; j++)
                            {
                                await jsonWriter.WriteStartObjectAsync().ConfigureAwait(false);
                                await jsonWriter.WritePropertyNameAsync("key").ConfigureAwait(false);
                                await jsonWriter.WriteValueAsync($"0x{storages[j].key}").ConfigureAwait(false);
                                await jsonWriter.WritePropertyNameAsync("value").ConfigureAwait(false);
                                await jsonWriter.WriteValueAsync($"0x{storages[j].value}").ConfigureAwait(false);
                                await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                            }
                            await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
                            await jsonWriter.WriteEndObjectAsync().ConfigureAwait(false);
                        }

                        if (contracts.Count > 1)
                            await jsonWriter.WriteEndArrayAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        if (contracts.Count == 0)
                        {
                            await writer.WriteLineAsync($"No contracts found matching the name {Contract}").ConfigureAwait(false);
                        }
                        else
                        {
                            for (int i = 0; i < contracts.Count; i++)
                            {
                                var storages = await expressNode.ListStoragesAsync(contracts[i].hash).ConfigureAwait(false);
                                await writer.WriteLineAsync($"contract:  {contracts[i].hash}").ConfigureAwait(false);
                                for (int j = 0; j < storages.Count; j++)
                                {
                                    await writer.WriteLineAsync($"  key:     0x{storages[j].key}").ConfigureAwait(false);
                                    await writer.WriteLineAsync($"    value: 0x{storages[j].value}").ConfigureAwait(false);
                                }
                            }
                        }
                    }
                }

                internal async Task ExecuteAsync(TextWriter writer)
                {
                    var (chainManager, _) = chainManagerFactory.LoadChain(Input);
                    var expressNode = chainManager.GetExpressNode();

                    if (UInt160.TryParse(Contract, out var hash))
                    {
                        await WriteStoragesAsync(expressNode, writer, new (UInt160, ContractManifest)[] { (hash, null!) }).ConfigureAwait(false);
                    }
                    else
                    {
                        var contracts = await expressNode.ListContractsAsync(Contract).ConfigureAwait(false);
                        await WriteStoragesAsync(expressNode, writer, contracts).ConfigureAwait(false);
                    }
                }

                internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
                {
                    try
                    {
                        await ExecuteAsync(console.Out).ConfigureAwait(false);
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        app.WriteException(ex);
                        return 1;
                    }
                }
            }

            [Command("update", Description = "Update the storage of a contract given the new key-value pair")]
            public class StorageUpdateKeyValue
            {
                readonly ExpressChainManagerFactory chainManagerFactory;

                public StorageUpdateKeyValue(ExpressChainManagerFactory chainManagerFactory)
                {
                    this.chainManagerFactory = chainManagerFactory;
                }

                [Argument(0, Description = "Contract name or invocation hash")]
                [Required]
                internal string Contract { get; init; } = string.Empty;

                [Argument(1, Description = "Storage key to update")]
                [Required]
                internal string Key { get; init; } = string.Empty;

                [Argument(2, Description = "Storage value to update")]
                [Required]
                internal string Value { get; init; } = string.Empty;

                [Option(Description = "Path to neo-express data file")]
                internal string Input { get; init; } = string.Empty;

                internal static async Task ExecuteAsync(ExpressChainManager chainManager, string contract, string key, string value, TextWriter writer)
                {
                    var expressNode = chainManager.GetExpressNode();
                    ContractParameterParser parser = await expressNode.GetContractParameterParserAsync(chainManager.Chain).ConfigureAwait(false);
                    var scriptHash = parser.TryLoadScriptHash(contract, out var hash)
                        ? hash
                        : UInt160.TryParse(contract, out var uint160)
                            ? uint160
                            : throw new InvalidOperationException($"contract \"{contract}\" not found");

                    var internalPair = (
                        ConvertArg(parser, key),
                        ConvertArg(parser, value)
                        );
                    await expressNode.PersistStorageKeyValueAsync(scriptHash, internalPair);

                    static string ConvertArg(ContractParameterParser parser, string arg)
                    {
                        var parameter = parser.ParseStringParameter(arg);
                        var paramValue = parameter.Value;

                        byte[] result;
                        if (paramValue is string strValue)
                        {
                            try
                            {
                                var fromBase64 = Convert.FromBase64String(strValue);
                                return strValue;
                            }
                            catch
                            {
                                if (BigInteger.TryParse(strValue, out var integerValue))
                                {
                                    result = integerValue.ToByteArray();
                                }
                                else if (bool.TryParse(strValue, out var boolValue))
                                {
                                    result = new byte[] { Convert.ToByte(boolValue) };
                                }
                                else
                                {
                                    result = Encoding.ASCII.GetBytes(strValue);
                                }
                            }
                        }
                        else if (paramValue is byte[] value)
                        {
                            result = value;
                        }
                        else if (paramValue is UInt160 hashValue)
                        {
                            result = ((byte[])parser.ParseStringParameter(hashValue.ToString()).Value).Reverse().ToArray();
                        }
                        else
                        {
                            result = Encoding.ASCII.GetBytes(parameter.ToString());
                        }
                        return Convert.ToBase64String(result);
                    }
                }

                internal async Task<int> OnExecuteAsync(CommandLineApplication app, IConsole console)
                {
                    try
                    {
                        var (chainManager, _) = chainManagerFactory.LoadChain(Input);

                        if (chainManager.Chain.ConsensusNodes.Count != 1)
                        {
                            throw new ArgumentException("Contract storage manipulation is only supported for single-node consensus");
                        }

                        await ExecuteAsync(chainManager, Contract, Key, Value, console.Out).ConfigureAwait(false);
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        app.WriteException(ex);
                        return 1;
                    }
                }
            }
        }
    }
}
