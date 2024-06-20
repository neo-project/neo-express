// Copyright (C) 2015-2024 The Neo Project.
//
// Extensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.BlockchainToolkit;
using Neo.BlockchainToolkit.Models;
using Neo.Persistence;
using Neo.SmartContract.Native;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;

namespace Neo.Test.Runner
{
    static class Extensions
    {
        public static ContractParameterParser CreateContractParameterParser(this IReadOnlyStore store, ExpressChain chain, IFileSystem? fileSystem = null)
        {
            var tryGetContract = CreateTryGetContract(store);
            return CreateContractParameterParser(chain, tryGetContract, fileSystem);
        }

        public static ContractParameterParser CreateContractParameterParser(this ExpressChain chain, ContractParameterParser.TryGetUInt160 tryGetContract, IFileSystem? fileSystem = null)
        {
            ContractParameterParser.TryGetUInt160 tryGetAccount = (string name, [MaybeNullWhen(false)] out UInt160 scriptHash) =>
                {
                    if (chain.TryGetDefaultAccount(name, out var account))
                    {
                        scriptHash = Neo.Wallets.Helper.ToScriptHash(account.ScriptHash, chain.AddressVersion);
                        return true;
                    }

                    scriptHash = null!;
                    return false;
                };

            return new ContractParameterParser(chain.AddressVersion,
                                               tryGetAccount: tryGetAccount,
                                               tryGetContract: tryGetContract,
                                               fileSystem: fileSystem);
        }

        public static ContractParameterParser CreateContractParameterParser(this IReadOnlyStore store, ProtocolSettings settings, IFileSystem? fileSystem = null)
        {
            var tryGetContract = CreateTryGetContract(store);
            return CreateContractParameterParser(settings, tryGetContract, fileSystem);
        }

        public static ContractParameterParser CreateContractParameterParser(this ProtocolSettings settings, ContractParameterParser.TryGetUInt160 tryGetContract, IFileSystem? fileSystem = null)
        {
            return new ContractParameterParser(settings.AddressVersion,
                                               tryGetAccount: null,
                                               tryGetContract: tryGetContract,
                                               fileSystem: fileSystem);
        }

        public static ContractParameterParser.TryGetUInt160 CreateTryGetContract(this IReadOnlyStore store)
        {
            (string name, UInt160 hash)[] contracts;
            using (var snapshot = new SnapshotCache(store))
            {
                contracts = NativeContract.ContractManagement.ListContracts(snapshot)
                    .Select(c => (name: c.Manifest.Name, hash: c.Hash))
                    .ToArray();
            }

            return (string name, [MaybeNullWhen(false)] out UInt160 scriptHash) =>
                {
                    for (int i = 0; i < contracts.Length; i++)
                    {
                        if (string.Equals(contracts[i].name, name))
                        {
                            scriptHash = contracts[i].hash;
                            return true;
                        }
                    }

                    for (int i = 0; i < contracts.Length; i++)
                    {
                        if (string.Equals(contracts[i].name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            scriptHash = contracts[i].hash;
                            return true;
                        }
                    }

                    scriptHash = null!;
                    return false;
                };
        }
    }
}
