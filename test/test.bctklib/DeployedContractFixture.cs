// Copyright (C) 2015-2023 The Neo Project.
//
// DeployedContractFixture.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.BlockchainToolkit.SmartContract;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Linq;
using System.Numerics;

namespace test.bctklib;

using static Utility;

public class DeployedContractFixture : IDisposable
{
    readonly IStore store;
    readonly ContractState state;
    public IReadOnlyStore Store => store;
    public UInt160 ContractHash => state.Hash;

    public DeployedContractFixture()
    {
        var nefFile = GetResourceNef("registrar.nef");
        var manifest = GetResourceManifest("registrar.manifest.json");
        var address = Neo.Wallets.Helper.ToScriptHash("NSGh7RQqCV7arpJjijqqHnT9cH6rJTGvYh", ProtocolSettings.Default.AddressVersion);
        var signer = new Signer() { Account = address };

        using var store = new MemoryStore();
        EnsureLedgerInitialized(store, ProtocolSettings.Default);

        using (var snapshot = new SnapshotCache(store.GetSnapshot()))
        {
            this.state = Deploy(snapshot, signer, nefFile, manifest, ProtocolSettings.Default);
            snapshot.Commit();
        }

        this.store = store;

        static NefFile GetResourceNef(string resourceName)
        {
            using var stream = GetResourceStream(resourceName);
            using var ms = new System.IO.MemoryStream();
            stream.CopyTo(ms);
            var reader = new MemoryReader(ms.ToArray());
            return reader.ReadSerializable<NefFile>();
        }

        static ContractManifest GetResourceManifest(string resourceName)
        {
            var json = GetResource(resourceName);
            return ContractManifest.Parse(json);
        }

        static void EnsureLedgerInitialized(IStore store, ProtocolSettings settings)
        {
            using SnapshotCache snapshotCache = new(store.GetSnapshot());

            if (LedgerInitialized(snapshotCache))
            {
                return;
            }

            Block block = NeoSystem.CreateGenesisBlock(settings);
            if (block.Transactions.Length != 0)
            {
                throw new Exception("Unexpected Transactions in genesis block");
            }

            using (ApplicationEngine applicationEngine = ApplicationEngine.Create(TriggerType.OnPersist, null, snapshotCache, block, settings, 0L))
            {
                using ScriptBuilder scriptBuilder = new();
                scriptBuilder.EmitSysCall(ApplicationEngine.System_Contract_NativeOnPersist);
                applicationEngine.LoadScript(scriptBuilder.ToArray());
                if (applicationEngine.Execute() != VMState.HALT)
                {
                    throw new InvalidOperationException("NativeOnPersist operation failed", applicationEngine.FaultException);
                }
            }

            using (ApplicationEngine applicationEngine2 = ApplicationEngine.Create(TriggerType.PostPersist, null, snapshotCache, block, settings, 0L))
            {
                using ScriptBuilder scriptBuilder2 = new();
                scriptBuilder2.EmitSysCall(ApplicationEngine.System_Contract_NativePostPersist);
                applicationEngine2.LoadScript(scriptBuilder2.ToArray());
                if (applicationEngine2.Execute() != VMState.HALT)
                {
                    throw new InvalidOperationException("NativePostPersist operation failed", applicationEngine2.FaultException);
                }
            }

            snapshotCache.Commit();
        }

        static bool LedgerInitialized(DataCache snapshot)
        {
            byte[] key_prefix = new KeyBuilder(NativeContract.Ledger.Id, 5).ToArray();
            return snapshot.Find(key_prefix).Any();
        }

        // following logic lifted from ContractManagement.Deploy
        static ContractState Deploy(DataCache snapshot, Signer deploySigner, NefFile nefFile, ContractManifest manifest, ProtocolSettings settings)
        {
            const byte Prefix_Contract = 8;

            Neo.SmartContract.Helper.Check(nefFile.Script, manifest.Abi);

            var hash = Neo.SmartContract.Helper.GetContractHash(deploySigner.Account, nefFile.CheckSum, manifest.Name);
            var key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_Contract).Add(hash);

            if (snapshot.Contains(key))
                throw new InvalidOperationException($"Contract Already Exists: {hash}");
            if (!manifest.IsValid(hash))
                throw new InvalidOperationException($"Invalid Manifest Hash: {hash}");

            var contract = new ContractState
            {
                Hash = hash,
                Id = GetNextAvailableId(snapshot),
                Manifest = manifest,
                Nef = nefFile,
                UpdateCounter = 0,
            };

            snapshot.Add(key, new StorageItem(contract));
            OnDeploy(contract, deploySigner, snapshot, settings, false);
            return contract;
        }

        // following logic lifted from ContractManagement.OnDeploy
        static void OnDeploy(ContractState contract, Signer deploySigner, DataCache snapshot, ProtocolSettings settings, bool update)
        {
            var deployMethod = contract.Manifest.Abi.GetMethod("_deploy", 2);
            if (deployMethod is not null)
            {
                var tx = TestApplicationEngine.CreateTestTransaction(deploySigner);
                using (var engine = ApplicationEngine.Create(TriggerType.Application, tx, snapshot, null, settings))
                {
                    var context = engine.LoadContract(contract, deployMethod, CallFlags.All);
                    context.EvaluationStack.Push(Neo.VM.Types.StackItem.Null);
                    context.EvaluationStack.Push(update ? Neo.VM.Types.StackItem.True : Neo.VM.Types.StackItem.False);
                    if (engine.Execute() != Neo.VM.VMState.HALT)
                        throw new InvalidOperationException("_deploy operation failed", engine.FaultException);
                }
            }
        }

        // following logic lifted from ContractManagement.GetNextAvailableId
        static int GetNextAvailableId(DataCache snapshot)
        {
            const byte Prefix_NextAvailableId = 15;

            var key = new KeyBuilder(NativeContract.ContractManagement.Id, Prefix_NextAvailableId);
            var item = snapshot.GetAndChange(key);
            int value = (int)(BigInteger)item;
            item.Add(1);
            return value;
        }
    }

    public void Dispose()
    {
        store.Dispose();
        GC.SuppressFinalize(this);
    }
}
