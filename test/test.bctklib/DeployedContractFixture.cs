// Copyright (C) 2015-2024 The Neo Project.
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
using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Persistence.Providers;
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
    public IStore Store => store;
    public UInt160 ContractHash => state.Hash;

    public static readonly ProtocolSettings Default = new()
    {
        Network = 0x334F454Eu,
        AddressVersion = ProtocolSettings.Default.AddressVersion,
        StandbyCommittee =
            [
                //Validators
                ECPoint.Parse("03b209fd4f53a7170ea4444e0cb0a6bb6a53c2bd016926989cf85f9b0fba17a70c", ECCurve.Secp256r1),
                ECPoint.Parse("02df48f60e8f3e01c48ff40b9b7f1310d7a8b2a193188befe1c2e3df740e895093", ECCurve.Secp256r1),
                ECPoint.Parse("03b8d9d5771d8f513aa0869b9cc8d50986403b78c6da36890638c3d46a5adce04a", ECCurve.Secp256r1),
                ECPoint.Parse("02ca0e27697b9c248f6f16e085fd0061e26f44da85b58ee835c110caa5ec3ba554", ECCurve.Secp256r1),
                ECPoint.Parse("024c7b7fb6c310fccf1ba33b082519d82964ea93868d676662d4a59ad548df0e7d", ECCurve.Secp256r1),
                ECPoint.Parse("02aaec38470f6aad0042c6e877cfd8087d2676b0f516fddd362801b9bd3936399e", ECCurve.Secp256r1),
                ECPoint.Parse("02486fd15702c4490a26703112a5cc1d0923fd697a33406bd5a1c00e0013b09a70", ECCurve.Secp256r1),
                //Other Members
                ECPoint.Parse("023a36c72844610b4d34d1968662424011bf783ca9d984efa19a20babf5582f3fe", ECCurve.Secp256r1),
                ECPoint.Parse("03708b860c1de5d87f5b151a12c2a99feebd2e8b315ee8e7cf8aa19692a9e18379", ECCurve.Secp256r1),
                ECPoint.Parse("03c6aa6e12638b36e88adc1ccdceac4db9929575c3e03576c617c49cce7114a050", ECCurve.Secp256r1),
                ECPoint.Parse("03204223f8c86b8cd5c89ef12e4f0dbb314172e9241e30c9ef2293790793537cf0", ECCurve.Secp256r1),
                ECPoint.Parse("02a62c915cf19c7f19a50ec217e79fac2439bbaad658493de0c7d8ffa92ab0aa62", ECCurve.Secp256r1),
                ECPoint.Parse("03409f31f0d66bdc2f70a9730b66fe186658f84a8018204db01c106edc36553cd0", ECCurve.Secp256r1),
                ECPoint.Parse("0288342b141c30dc8ffcde0204929bb46aed5756b41ef4a56778d15ada8f0c6654", ECCurve.Secp256r1),
                ECPoint.Parse("020f2887f41474cfeb11fd262e982051c1541418137c02a0f4961af911045de639", ECCurve.Secp256r1),
                ECPoint.Parse("0222038884bbd1d8ff109ed3bdef3542e768eef76c1247aea8bc8171f532928c30", ECCurve.Secp256r1),
                ECPoint.Parse("03d281b42002647f0113f36c7b8efb30db66078dfaaa9ab3ff76d043a98d512fde", ECCurve.Secp256r1),
                ECPoint.Parse("02504acbc1f4b3bdad1d86d6e1a08603771db135a73e61c9d565ae06a1938cd2ad", ECCurve.Secp256r1),
                ECPoint.Parse("0226933336f1b75baa42d42b71d9091508b638046d19abd67f4e119bf64a7cfb4d", ECCurve.Secp256r1),
                ECPoint.Parse("03cdcea66032b82f5c30450e381e5295cae85c5e6943af716cc6b646352a6067dc", ECCurve.Secp256r1),
                ECPoint.Parse("02cd5a5547119e24feaa7c2a0f37b8c9366216bab7054de0065c9be42084003c8a", ECCurve.Secp256r1)
            ],
        ValidatorsCount = 7,
        SeedList = [],
        MillisecondsPerBlock = ProtocolSettings.Default.MillisecondsPerBlock,
        MaxTransactionsPerBlock = ProtocolSettings.Default.MaxTransactionsPerBlock,
        MemoryPoolMaxTransactions = ProtocolSettings.Default.MemoryPoolMaxTransactions,
        MaxTraceableBlocks = ProtocolSettings.Default.MaxTraceableBlocks,
        InitialGasDistribution = ProtocolSettings.Default.InitialGasDistribution,
        Hardforks = ProtocolSettings.Default.Hardforks
    };

    public DeployedContractFixture()
    {
        var nefFile = GetResourceNef("registrar.nef");
        var manifest = GetResourceManifest("registrar.manifest.json");
        var address = Neo.Wallets.Helper.ToScriptHash("NSGh7RQqCV7arpJjijqqHnT9cH6rJTGvYh", ProtocolSettings.Default.AddressVersion);
        var signer = new Signer() { Account = address };

        using var store = new MemoryStore();

        EnsureLedgerInitialized(store, Default);

        using (var snapshot = new StoreCache(store.GetSnapshot()))
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

        void EnsureLedgerInitialized(IStore store, ProtocolSettings settings)
        {
            using StoreCache snapshotCache = new(store.GetSnapshot());

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
            if (!manifest.IsValid(ExecutionEngineLimits.Default, hash))
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
