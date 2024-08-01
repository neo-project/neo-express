// Copyright (C) 2015-2024 The Neo Project.
//
// TestApplicationEngine.cs file belongs to neo-express project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using OneOf;
using System.Collections.Immutable;
using System.IO.Abstractions;
using System.Reflection;
using ExecutionContext = Neo.VM.ExecutionContext;

namespace Neo.BlockchainToolkit.SmartContract
{
    using WitnessChecker = Func<byte[], bool>;

    public partial class TestApplicationEngine : ApplicationEngine
    {
        readonly static IReadOnlyDictionary<uint, InteropDescriptor> overriddenServices;

        static TestApplicationEngine()
        {
            var builder = ImmutableDictionary.CreateBuilder<uint, InteropDescriptor>();
            builder.Add(OverrideDescriptor(ApplicationEngine.System_Runtime_CheckWitness, nameof(CheckWitnessOverride)));
            overriddenServices = builder.ToImmutable();

            static KeyValuePair<uint, InteropDescriptor> OverrideDescriptor(InteropDescriptor descriptor, string overrideMethodName)
            {
                var overrideMethodInfo = typeof(TestApplicationEngine).GetMethod(overrideMethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException($"{nameof(OverrideDescriptor)} failed to locate {overrideMethodName} method");
                return KeyValuePair.Create(descriptor.Hash, descriptor with { Handler = overrideMethodInfo });
            }
        }

        public static Block CreateDummyBlock(DataCache snapshot, ProtocolSettings? settings = null)
        {
            settings ??= ProtocolSettings.Default;
            var hash = NativeContract.Ledger.CurrentHash(snapshot);
            var currentBlock = NativeContract.Ledger.GetBlock(snapshot, hash);

            return new Block
            {
                Header = new Header
                {
                    Version = 0,
                    PrevHash = hash,
                    MerkleRoot = UInt256.Zero,
                    Timestamp = currentBlock.Timestamp + settings.MillisecondsPerBlock,
                    Index = currentBlock.Index + 1,
                    NextConsensus = currentBlock.NextConsensus,
                    Witness = new Witness
                    {
                        InvocationScript = Array.Empty<byte>(),
                        VerificationScript = Array.Empty<byte>()
                    },
                },
                Transactions = Array.Empty<Transaction>()
            };
        }

        public static Transaction CreateTestTransaction(UInt160 signerAccount, WitnessScope witnessScope = WitnessScope.CalledByEntry)
            => CreateTestTransaction(new Signer
            {
                Account = signerAccount,
                Scopes = witnessScope,
                AllowedContracts = Array.Empty<UInt160>(),
                AllowedGroups = Array.Empty<Neo.Cryptography.ECC.ECPoint>()
            });

        public static Transaction CreateTestTransaction(Signer? signer = null) => new()
        {
            Nonce = (uint)new Random().Next(),
            Script = Array.Empty<byte>(),
            Signers = signer == null ? Array.Empty<Signer>() : new[] { signer },
            Attributes = Array.Empty<TransactionAttribute>(),
            Witnesses = Array.Empty<Witness>(),
        };

        const string COVERAGE_ENV_NAME = "NEO_TEST_APP_ENGINE_COVERAGE_PATH";
        record BranchInstructionInfo(UInt160 ContractHash, int InstructionPointer, int BranchOffset);

        readonly Dictionary<UInt160, OneOf<ContractState, Script>> executedScripts = new();
        readonly Dictionary<UInt160, Dictionary<int, int>> hitMaps = new();
        readonly Dictionary<UInt160, Dictionary<int, (int branchCount, int continueCount)>> branchMaps = new();
        readonly WitnessChecker witnessChecker;
        readonly IFileSystem? fileSystem;
        BranchInstructionInfo? branchInstructionInfo = null;
        CoverageWriter? coverageWriter = null;

        public IReadOnlyDictionary<UInt160, OneOf<ContractState, Script>> ExecutedScripts => executedScripts;

        public new event EventHandler<LogEventArgs>? Log;
        public new event EventHandler<NotifyEventArgs>? Notify;

        public TestApplicationEngine(DataCache snapshot, ProtocolSettings? settings = null)
            : this(snapshot, container: null, settings: settings)
        {
        }

        public TestApplicationEngine(DataCache snapshot, Transaction transaction, ProtocolSettings? settings = null)
            : this(snapshot, container: transaction, settings: settings)
        {
        }

        public TestApplicationEngine(DataCache snapshot, Signer signer, ProtocolSettings? settings = null)
            : this(snapshot, container: CreateTestTransaction(signer), settings: settings)
        {
        }

        public TestApplicationEngine(DataCache snapshot, UInt160 signer, WitnessScope witnessScope = WitnessScope.CalledByEntry, ProtocolSettings? settings = null)
            : this(snapshot, container: CreateTestTransaction(signer, witnessScope), settings: settings)
        {
        }

        public TestApplicationEngine(DataCache snapshot, ProtocolSettings settings, UInt160 signer, WitnessScope witnessScope = WitnessScope.CalledByEntry)
            : this(TriggerType.Application, CreateTestTransaction(signer, witnessScope), snapshot, null, settings, ApplicationEngine.TestModeGas, null)
        {
        }

        public TestApplicationEngine(DataCache snapshot, ProtocolSettings settings, Transaction transaction)
            : this(snapshot, container: transaction, settings: settings)
        {
        }

        public TestApplicationEngine(TriggerType trigger, IVerifiable? container, DataCache snapshot, Block? persistingBlock, ProtocolSettings settings, long gas, WitnessChecker? witnessChecker, IDiagnostic? diagnostic = null)
            : this(snapshot, trigger, container, persistingBlock, settings, gas, diagnostic, witnessChecker)
        {
        }

        public TestApplicationEngine(DataCache snapshot,
                                     TriggerType trigger = TriggerType.Application,
                                     IVerifiable? container = null,
                                     Block? persistingBlock = null,
                                     ProtocolSettings? settings = null,
                                     long gas = TestModeGas,
                                     IDiagnostic? diagnostic = null,
                                     WitnessChecker? witnessChecker = null,
                                     IFileSystem? fileSystem = null)
            : base(trigger,
                container ?? CreateTestTransaction(),
                snapshot,
                persistingBlock,
                settings ?? ProtocolSettings.Default,
                gas,
                diagnostic)
        {
            this.witnessChecker = witnessChecker ?? CheckWitness;
            this.fileSystem = fileSystem;
            ApplicationEngine.Log += OnLog;
            ApplicationEngine.Notify += OnNotify;
        }

        public override void Dispose()
        {
            coverageWriter?.Dispose();
            ApplicationEngine.Log -= OnLog;
            ApplicationEngine.Notify -= OnNotify;
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        public IReadOnlyDictionary<int, int> GetHitMap(UInt160 contractHash)
        {
            if (hitMaps.TryGetValue(contractHash, out var hitMap))
            {
                return hitMap;
            }

            return ImmutableDictionary<int, int>.Empty;
        }

        public IReadOnlyDictionary<int, (int branchCount, int continueCount)> GetBranchMap(UInt160 contractHash)
        {
            if (branchMaps.TryGetValue(contractHash, out var branchMap))
            {
                return branchMap;
            }

            return ImmutableDictionary<int, (int, int)>.Empty;
        }

        public override VMState Execute()
        {
            var coveragePath = Environment.GetEnvironmentVariable(COVERAGE_ENV_NAME);
            coverageWriter = string.IsNullOrEmpty(coveragePath)
                ? null
                : new CoverageWriter(coveragePath, fileSystem);
            coverageWriter?.WriteContext(CurrentContext);

            return base.Execute();
        }

        public override void LoadContext(ExecutionContext context)
        {
            base.LoadContext(context);
            coverageWriter?.WriteContext(context);

            var ecs = context.GetState<ExecutionContextState>();
            if (ecs.ScriptHash != null
                && !executedScripts.ContainsKey(ecs.ScriptHash))
            {
                executedScripts.Add(ecs.ScriptHash, ecs.Contract is null ? context.Script : ecs.Contract);
            }
        }

        protected override void PreExecuteInstruction(Instruction instruction)
        {
            branchInstructionInfo = null;
            base.PreExecuteInstruction(instruction);

            // if there's no current context, there's no instruction pointer to record
            if (CurrentContext is null)
                return;

            var ip = CurrentContext.InstructionPointer;
            coverageWriter?.WriteAddress(ip);

            var hash = CurrentContext.GetScriptHash();
            // if the current context has no script hash, there's no key for the hit or branch map
            if (hash is null)
                return;

            if (!hitMaps.TryGetValue(hash, out var hitMap))
            {
                hitMap = new Dictionary<int, int>();
                hitMaps.Add(hash, hitMap);
            }
            hitMap[ip] = hitMap.TryGetValue(ip, out var _hitCount) ? _hitCount + 1 : 1;

            var offset = GetBranchOffset(instruction);
            if (offset != 0)
            {
                branchInstructionInfo = new BranchInstructionInfo(hash, ip, ip + offset);
            }

            static int GetBranchOffset(Instruction instruction)
                => instruction.OpCode switch
                {
                    OpCode.JMPIF_L or OpCode.JMPIFNOT_L or
                    OpCode.JMPEQ_L or OpCode.JMPNE_L or
                    OpCode.JMPGT_L or OpCode.JMPGE_L or
                    OpCode.JMPLT_L or OpCode.JMPLE_L => instruction.TokenI32,
                    OpCode.JMPIF or OpCode.JMPIFNOT or
                    OpCode.JMPEQ or OpCode.JMPNE or
                    OpCode.JMPGT or OpCode.JMPGE or
                    OpCode.JMPLT or OpCode.JMPLE => instruction.TokenI8,
                    _ => 0
                };
        }

        protected override void PostExecuteInstruction(Instruction instruction)
        {
            base.PostExecuteInstruction(instruction);

            // if branchInstructionInfo is null, instruction is not a branch instruction
            if (branchInstructionInfo is null
                // if there's no current context, there's no instruction pointer to record
                || CurrentContext == null)
                return;

            var (hash, branchIP, offsetIP) = branchInstructionInfo;
            var currentIP = CurrentContext.InstructionPointer;

            coverageWriter?.WriteBranch(branchIP, offsetIP, currentIP);

            if (!branchMaps.TryGetValue(hash, out var branchMap))
            {
                branchMap = new Dictionary<int, (int, int)>();
                branchMaps.Add(branchInstructionInfo.ContractHash, branchMap);
            }

            var (branchCount, continueCount) = branchMap.TryGetValue(offsetIP, out var value)
                ? value : (0, 0);

            if (currentIP == offsetIP)
            {
                branchMap[branchIP] = (branchCount + 1, continueCount);
            }
            else if (currentIP == branchIP)
            {
                branchMap[branchIP] = (branchCount, continueCount + 1);
            }
        }

        private void OnLog(object? sender, LogEventArgs args)
        {
            if (ReferenceEquals(this, sender))
            {
                this.Log?.Invoke(sender, args);
            }
        }

        private void OnNotify(object? sender, NotifyEventArgs args)
        {
            if (ReferenceEquals(this, sender))
            {
                this.Notify?.Invoke(sender, args);
            }
        }

        bool CheckWitnessOverride(byte[] hashOrPubkey) => witnessChecker(hashOrPubkey);

        protected override void OnSysCall(InteropDescriptor descriptor)
        {
            if (overriddenServices.TryGetValue(descriptor, out var overrideDescriptor))
            {
                base.OnSysCall(overrideDescriptor);
            }
            else
            {
                base.OnSysCall(descriptor);
            }
        }
    }
}
