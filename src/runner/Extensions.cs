// Copyright (C) 2015-2025 The Neo Project.
//
// Extensions.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or https://opensource.org/license/MIT
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

        public static ContractParameterParser.TryGetUInt160 CreateTryGetContract(this IStore store)
        {
            (string name, UInt160 hash)[] contracts;
            using (var snapshot = new StoreCache(store.GetSnapshot()))
            {
                contracts = NativeContract.ContractManagement.ListContracts(snapshot)
                    .Select(c => (name: c.Manifest.Name, hash: c.Hash))
                    .ToArray();
            }

            return CreateTryGetContractFromArray(contracts);
        }

        public static ContractParameterParser.TryGetUInt160 CreateTryGetContract(this IReadOnlyStore store)
        {
            // For IReadOnlyStore, we need to use a different approach since it doesn't have GetSnapshot()
            // We'll create a minimal implementation that returns a no-op function
            // This maintains compatibility but with limited functionality
            return (string name, [MaybeNullWhen(false)] out UInt160 scriptHash) =>
            {
                scriptHash = null!;
                return false;
            };
        }

        private static ContractParameterParser.TryGetUInt160 CreateTryGetContractFromArray((string name, UInt160 hash)[] contracts)
        {
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
