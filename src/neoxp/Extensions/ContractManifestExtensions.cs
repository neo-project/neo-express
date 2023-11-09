// Copyright (C) 2015-2023 The Neo Project.
//
// The neo is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract;
using Neo.SmartContract.Manifest;

namespace NeoExpress;

internal static class ContractManifestExtensions
{
    public static bool IsNep17Compliant(this ContractManifest manifest)
    {
        try
        {
            var symbolMethod = manifest.Abi.GetMethod("symbol", 0);
            var decimalsMethod = manifest.Abi.GetMethod("decimals", 0);
            var totalSupplyMethod = manifest.Abi.GetMethod("totalSupply", 0);
            var balanceOfMethod = manifest.Abi.GetMethod("balanceOf", 1);
            var transferMethod = manifest.Abi.GetMethod("transfer", 4);

            var symbolValid = symbolMethod.Safe == true &&
                symbolMethod.ReturnType == ContractParameterType.String;
            var decimalsValid = decimalsMethod.Safe == true &&
                decimalsMethod.ReturnType == ContractParameterType.Integer;
            var totalSupplyValid = totalSupplyMethod.Safe == true &&
                totalSupplyMethod.ReturnType == ContractParameterType.Integer;
            var balanceOfValid = balanceOfMethod.Safe == true &&
                balanceOfMethod.ReturnType == ContractParameterType.Integer &&
                balanceOfMethod.Parameters[0].Type == ContractParameterType.Hash160;
            var transferValid = transferMethod.Safe == false &&
                transferMethod.ReturnType == ContractParameterType.Boolean &&
                transferMethod.Parameters[0].Type == ContractParameterType.Hash160 &&
                transferMethod.Parameters[1].Type == ContractParameterType.Hash160 &&
                transferMethod.Parameters[2].Type == ContractParameterType.Integer &&
                transferMethod.Parameters[3].Type == ContractParameterType.Any;
            var transferEvent = manifest.Abi.Events.SingleOrDefault(s =>
                s.Name == "transfer" &&
                s.Parameters.Length == 3 &&
                s.Parameters[0].Type == ContractParameterType.Hash160 &&
                s.Parameters[1].Type == ContractParameterType.Hash160 &&
                s.Parameters[2].Type == ContractParameterType.Integer) != null;

            return (symbolValid &&
                decimalsValid &&
                totalSupplyValid &&
                balanceOfValid &&
                transferValid &&
                transferEvent);
        }
        catch
        {
            return false;
        }
    }
}
